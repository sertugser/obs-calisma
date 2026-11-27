using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SIS.Application.Auth.DTOs;
using SIS.Application.Common.Services;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WhenPasswordRotationExpired_ReturnsPasswordChangeRequired()
    {
        var user = new ApplicationUser
        {
            Id = 42,
            Email = "instructor@ankarabilim.edu.tr",
            UserName = "instructor@ankarabilim.edu.tr",
            FirstName = "Ada",
            LastName = "Lovelace",
            PasswordChangedAtUtc = DateTime.UtcNow.AddMonths(-8)
        };

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
        userManager.Setup(x => x.CheckPasswordAsync(user, "Valid123!")).ReturnsAsync(true);
        userManager.Setup(x => x.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Instructor" });

        var config = CreateSystemConfigMock();
        config.Setup(x => x.GetBoolAsync("Security.PasswordRotationEnabled", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        config.Setup(x => x.GetIntAsync("Security.PasswordRotationMonths", 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);

        var service = new AuthService(userManager.Object, CreateConfiguration(), config.Object, CreateContext(), NullLogger<AuthService>.Instance);

        var response = await service.LoginAsync(new LoginDto
        {
            Email = user.Email,
            Password = "Valid123!"
        });

        response.Success.Should().BeTrue();
        response.PasswordChangeRequired.Should().BeTrue();
        response.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePasswordAsync_UpdatesTimestamp_AndClearsPasswordChangeRequirement()
    {
        var user = new ApplicationUser
        {
            Id = 42,
            Email = "instructor@ankarabilim.edu.tr",
            UserName = "instructor@ankarabilim.edu.tr",
            FirstName = "Ada",
            LastName = "Lovelace",
            PasswordChangedAtUtc = DateTime.UtcNow.AddMonths(-8)
        };

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByIdAsync("42")).ReturnsAsync(user);
        userManager.Setup(x => x.ChangePasswordAsync(user, "Old123!", "NewPassword123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Instructor" });

        var service = new AuthService(userManager.Object, CreateConfiguration(), CreateSystemConfigMock().Object, CreateContext(), NullLogger<AuthService>.Instance);

        var response = await service.ChangePasswordAsync(42, new ChangePasswordDto
        {
            CurrentPassword = "Old123!",
            NewPassword = "NewPassword123!"
        });

        response.Success.Should().BeTrue();
        response.PasswordChangeRequired.Should().BeFalse();
        user.PasswordChangedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // D46: kullanıcı güncellemesi (yanlış parolada sayaç artışı, doğru parolada sıfırlama) eşzamanlılık hatası dönerse kullanıcı
    // yeniden yüklenir ve işlem yeniden denenir; yanıt bugünkü yanıttır, uyarı günlüğü yok.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoginAsync_UserUpdateConcurrencyFailureThenSuccess_RetriesOnce_ResponseUnchanged(bool passwordValid)
    {
        var (service, userManager, logger, user) = CreateLoginService(passwordValid);
        UserUpdate(userManager, user, passwordValid).ReturnsAsync(ConcurrencyFailure()).ReturnsAsync(IdentityResult.Success);

        var response = await service.LoginAsync(new LoginDto { Email = user.Email! });

        response.Success.Should().Be(passwordValid);
        response.Error.Should().Be(passwordValid ? null : "Invalid email or password.");
        VerifyUserUpdate(userManager, user, passwordValid, Times.Exactly(2));
        VerifyWarnings(logger, Times.Never());
    }

    // Her denemede eşzamanlılık hatası: tam üç deneme, istisna yok, yanıt bugünkü yanıt, tek uyarı günlüğü.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoginAsync_UserUpdateConcurrencyFailureEveryAttempt_ThreeAttempts_ResponseUnchanged_LogsWarningOnce(bool passwordValid)
    {
        var (service, userManager, logger, user) = CreateLoginService(passwordValid);
        UserUpdate(userManager, user, passwordValid).ReturnsAsync(ConcurrencyFailure()).ReturnsAsync(ConcurrencyFailure()).ReturnsAsync(ConcurrencyFailure());

        var response = await service.LoginAsync(new LoginDto { Email = user.Email! });

        response.Success.Should().Be(passwordValid);
        response.Error.Should().Be(passwordValid ? null : "Invalid email or password.");
        VerifyUserUpdate(userManager, user, passwordValid, Times.Exactly(3));
        VerifyWarnings(logger, Times.Once());
    }

    // Doğru parolada sıfırlama çakışır ve yeniden yüklenen hesap kilitliyse: sıfırlama yeniden denenmez, yanıt kilitli hesabın
    // bugünkü yanıtı, jeton yok, uyarı günlüğü yok.
    [Fact]
    public async Task LoginAsync_ResetConcurrencyFailure_ReloadedUserLockedOut_ReturnsLockedResponse_NoRetry()
    {
        var (service, userManager, logger, user) = CreateLoginService(passwordValid: true);
        userManager.SetupSequence(x => x.IsLockedOutAsync(user)).ReturnsAsync(false).ReturnsAsync(true);
        UserUpdate(userManager, user, passwordValid: true).ReturnsAsync(ConcurrencyFailure()).ReturnsAsync(IdentityResult.Success);

        var response = await service.LoginAsync(new LoginDto { Email = user.Email! });

        response.Success.Should().BeFalse();
        response.Error.Should().Be("Account is locked. Please try again later.");
        response.Token.Should().BeEmpty();
        VerifyUserUpdate(userManager, user, passwordValid: true, Times.Once());
        VerifyWarnings(logger, Times.Never());
    }

    private static (AuthService, Mock<UserManager<ApplicationUser>>, Mock<ILogger<AuthService>>, ApplicationUser) CreateLoginService(bool passwordValid)
    {
        var context = CreateContext();
        var user = new ApplicationUser { Id = 42, Email = "student@example.test", UserName = "student@example.test", FirstName = "Ada", LastName = "Lovelace", PasswordChangedAtUtc = DateTime.UtcNow };
        context.Users.Add(user);
        context.SaveChanges();
        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
        userManager.Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(passwordValid);
        userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Student" });
        var logger = new Mock<ILogger<AuthService>>();
        return (new AuthService(userManager.Object, CreateConfiguration(), CreateSystemConfigMock().Object, context, logger.Object), userManager, logger, user);
    }

    private static Moq.Language.ISetupSequentialResult<Task<IdentityResult>> UserUpdate(Mock<UserManager<ApplicationUser>> userManager, ApplicationUser user, bool passwordValid) =>
        passwordValid ? userManager.SetupSequence(x => x.ResetAccessFailedCountAsync(user)) : userManager.SetupSequence(x => x.AccessFailedAsync(user));

    private static void VerifyUserUpdate(Mock<UserManager<ApplicationUser>> userManager, ApplicationUser user, bool passwordValid, Times times)
    {
        if (passwordValid) userManager.Verify(x => x.ResetAccessFailedCountAsync(user), times);
        else userManager.Verify(x => x.AccessFailedAsync(user), times);
    }

    private static void VerifyWarnings(Mock<ILogger<AuthService>> logger, Times times) =>
        logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    private static IdentityResult ConcurrencyFailure() => IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserPasswordStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-test-key-that-is-long-enough",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiresInMinutes"] = "60"
            })
            .Build();

    private static Mock<ISystemConfigProvider> CreateSystemConfigMock()
    {
        var mock = new Mock<ISystemConfigProvider>();
        mock.Setup(x => x.GetBoolAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mock.Setup(x => x.GetIntAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);
        return mock;
    }
}
