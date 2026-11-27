using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SIS.API.Security;
using SIS.Application.AcademicOperations.Services;
using SIS.Application.Admin.Services;
using SIS.Application.AcademicCalendar.Services;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Application.Identity.Services;
using SIS.Application.Auth.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.ElectiveGroups.Services;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.UserPreferences.Services;
using SIS.Application.EnrollmentForecast.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.Scheduling.Services;
using SIS.Application.StudentEligibility.Services;
using SIS.Application.CalendarGenerator.Services;
using SIS.Application.AcademicLevel.Services;
using SIS.Infrastructure.Services.Scheduling;
using SIS.Application.AcademicProgression.Services;
using SIS.Application.CourseOpening.Services;
using SIS.Application.FacultyPlanning.Services;
using SIS.Application.CourseEvaluation.Services;
using SIS.Application.Announcement.Attachments;
using SIS.Application.Announcement.Configuration;
using SIS.Application.Announcement.Security;
using SIS.Application.Announcement.Services;
using SIS.Application.StudentCard.Services;
using SIS.Application.Integration;
using SIS.Infrastructure.BackgroundServices;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;
using SIS.Infrastructure.Services.Scope;
using SIS.Infrastructure.Services.CourseEvaluation;
using SIS.Infrastructure.Services.Announcement;
using SIS.Infrastructure.Services.CalendarGenerator;
using SIS.Infrastructure.Services.Integration;

// `setup` komutu sunucuyu başlatmaz; aynı servislerle kurulum adımlarını çalıştırıp çıkar.
var kurulumModu = SIS.Infrastructure.Persistence.Kurulum.KurulumSecenekleri.KurulumKomutuMu(args);
var builder = WebApplication.CreateBuilder(kurulumModu ? [] : args);
StartupSecurityValidator.Validate(builder.Configuration, builder.Environment);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddScoped<SIS.API.Filters.SensitiveDataAccessFilter>();
builder.Services.AddScoped<SIS.API.Authorization.IAcademicScopeGuard, SIS.API.Authorization.AcademicScopeGuard>();

// Rate limiting for audit log endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthLogin", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 40,
                QueueLimit = 0,
            }));
    options.AddPolicy("AuthLoginSettings", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 120,
                QueueLimit = 0,
            }));
    options.AddPolicy("PublicRead", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 60,
                QueueLimit = 0,
            }));
    options.AddPolicy("PublicAttendanceQr", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 20,
                QueueLimit = 0,
            }));
    options.AddPolicy("AuditLogRead", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 30,
                QueueLimit = 0,
            }));
    options.AddPolicy("AuditLogExport", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 3,
                QueueLimit = 0,
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
});
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditInterceptor>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add EF Core + PostgreSQL
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>());
});

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.Lockout.MaxFailedAccessAttempts = 5; // FR-07
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = StartupSecurityValidator.GetRequiredJwtKey(builder.Configuration);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // SignalR WebSocket bağlantısı header gönderemez — token query param'dan gelir.
    // Bu event sadece /hubs/* path'lerinde devreye girer; HTTP API çağrıları
    // standart Authorization header'ını kullanmaya devam eder.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// SignalR — auto-schedule progress, real-time bildirimler vb. için.
builder.Services.AddSignalR();
builder.Services.AddSingleton<SIS.Application.Scheduling.Services.IScheduleProgressReporter,
    SIS.API.Hubs.SignalRScheduleProgressReporter>();
// Cancel registry: aktif auto-schedule run'larının CTS'ini kullanıcı bazında tutar.
// Singleton — tüm hub instance'ları aynı registry üzerinden çalışır.
builder.Services.AddSingleton<SIS.Application.Scheduling.Services.IScheduleCancelRegistry,
    SIS.Infrastructure.Services.Scheduling.ScheduleCancelRegistry>();
// Bildirim gerçek-zamanlı push (SignalR). IHubContext singleton-safe → Singleton.
builder.Services.AddSingleton<SIS.Application.Notifications.Services.INotificationRealtimePush,
    SIS.API.Hubs.SignalRNotificationPush>();

// Register Auth services
builder.Services.AddScoped<IAuthService, AuthService>();

// Elective Groups
builder.Services.AddScoped<IElectiveGroupScopeValidator, ElectiveGroupScopeValidator>();

// Register Course Registration services
builder.Services.AddScoped<IRegistrationValidationService, RegistrationValidationService>();
builder.Services.AddScoped<IAdvisorConstraintService, AdvisorConstraintService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<ICourseRegistrationService, CourseRegistrationService>();
builder.Services.AddScoped<ICurriculumStatusService, CurriculumStatusService>();
builder.Services.AddScoped<SIS.Application.Curriculum.Services.ICourseStatisticsService, SIS.Infrastructure.Services.CourseStatisticsService>();
builder.Services.AddScoped<SIS.Application.CourseRegistration.Services.IEctsLimitResolver, SIS.Infrastructure.Services.EctsLimitResolver>();
builder.Services.AddScoped<SIS.Application.CourseRegistration.Services.IRegistrationGate, SIS.Infrastructure.Services.RegistrationGate>();
builder.Services.AddScoped<SIS.Application.CourseRegistration.Services.IElectivePoolProgressCalculator, SIS.Infrastructure.Services.ElectivePoolProgressCalculator>();
builder.Services.AddScoped<SIS.Application.CourseRegistration.Services.IStudentRegistrationCatalogService, SIS.Infrastructure.Services.StudentRegistrationCatalogService>();
builder.Services.AddScoped<SIS.Application.CourseRegistration.Services.ISectionPickerService, SIS.Infrastructure.Services.SectionPickerService>();
builder.Services.AddScoped<IStudentTranscriptService, StudentTranscriptService>();
builder.Services.AddScoped<SIS.Application.StudentCard.Services.ITranscriptScenarioService, SIS.Infrastructure.Services.TranscriptScenarioService>();
builder.Services.AddScoped<SIS.Application.Notifications.Services.INotificationService, SIS.Infrastructure.Services.NotificationService>();
builder.Services.AddScoped<ICourseHistoryPdfService, CourseHistoryPdfService>();
builder.Services.AddScoped<IRegistrationPlanPdfService, RegistrationPlanPdfService>();
builder.Services.AddHostedService<RegistrationCleanupJob>(); // FR-44 (gece Draft) + add/drop (Draft+Pending) tek job
builder.Services.AddHostedService<EvaluationPeriodOpenerJob>();
builder.Services.AddHostedService<AcademicTermActivationReminderJob>();
builder.Services.AddHostedService<AuditRetentionService>();

// Register GPA Calculation service (+ shared grade-visibility used by GPA & transcript)
builder.Services.AddScoped<SIS.Application.Common.Services.IGradeVisibilityService, SIS.Infrastructure.Services.GradeVisibilityService>();
builder.Services.AddScoped<IGpaCalculationService, GpaCalculationService>();
builder.Services.AddScoped<SIS.Application.Grading.IExamRegulationResolver, SIS.Infrastructure.Services.ExamRegulationResolver>();
builder.Services.AddScoped<SIS.Application.Grading.IAttendancePolicyResolver, SIS.Infrastructure.Services.AttendancePolicyResolver>();
builder.Services.AddScoped<EnrollmentForecastService>();
builder.Services.AddScoped<IEnrollmentForecastService>(serviceProvider => serviceProvider.GetRequiredService<EnrollmentForecastService>());
builder.Services.AddScoped<IEnrollmentForecastCacheInvalidator>(serviceProvider => serviceProvider.GetRequiredService<EnrollmentForecastService>());
builder.Services.AddSchedulingConstraints();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<ISchedulingService>(sp => sp.GetRequiredService<SchedulingService>());
builder.Services.AddScoped<SIS.Application.Scheduling.Services.IStudentTimetableService, SIS.Infrastructure.Services.Scheduling.StudentTimetableService>();
builder.Services.AddScoped<SIS.Application.Scheduling.Services.IDepartmentScheduleService, SIS.Infrastructure.Services.Scheduling.DepartmentScheduleService>();
builder.Services.AddScoped<ITimeSlotService, TimeSlotService>();
builder.Services.AddScoped<ClassroomSuggestionService>();
builder.Services.AddScoped<IFacultyPlanningService, FacultyPlanningService>();
builder.Services.AddScoped<IAcademicOperationsService, AcademicOperationsService>();
builder.Services.AddScoped<SIS.Application.AdvisorInfo.Services.IAdvisorInfoService, SIS.Infrastructure.Services.AdvisorInfoService>();
builder.Services.AddScoped<SIS.Application.OpenedDepartmentCourses.Services.IOpenedDepartmentCourseService, SIS.Infrastructure.Services.OpenedDepartmentCourseService>();
builder.Services.AddScoped<IStudentEligibilityService, StudentEligibilityService>();
builder.Services.AddScoped<IAcademicLevelService, AcademicLevelService>();
builder.Services.AddScoped<IAcademicProgressionService, AcademicProgressionService>();
// Faz 3.C: Eski monolitik CourseOpeningService 4 ayrı servise + 1 helper'a bölündü
builder.Services.AddScoped<SIS.Infrastructure.Services.CourseOpening.CourseOpeningHelpers>();
builder.Services.AddScoped<ICourseSectionLifecycleService, SIS.Infrastructure.Services.CourseOpening.CourseSectionLifecycleService>();
builder.Services.AddScoped<ICourseOpeningQueryService, SIS.Infrastructure.Services.CourseOpening.CourseOpeningQueryService>();
builder.Services.AddScoped<IInstructorAssignmentService, SIS.Infrastructure.Services.CourseOpening.InstructorAssignmentService>();
builder.Services.AddScoped<ITermCourseTransferService, SIS.Infrastructure.Services.CourseOpening.TermCourseTransferService>();
builder.Services.AddScoped<IExcelScheduleService, ExcelScheduleService>();

// Admin services
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminStudentService, AdminStudentService>();
builder.Services.AddScoped<IStudentPersonalInfoService, StudentPersonalInfoService>();
builder.Services.AddScoped<IStudentScholarshipService, StudentScholarshipService>();
builder.Services.AddScoped<IStudentPlacementService, StudentPlacementService>();
builder.Services.AddScoped<IExternalIdentityService, MockNviIdentityService>();
builder.Services.AddScoped<IExternalStudentReportingService, MockYoksisReportingService>();
builder.Services.AddScoped<IStudentDocumentService, StudentDocumentService>();
builder.Services.AddScoped<IIssuedDocumentService, IssuedDocumentService>();
builder.Services.AddScoped<IStudentDocumentPdfService, StudentDocumentPdfService>();
// QuestPDF Community lisansı bir kez ayarlanır (paylaşılan statik state'e her istekte yazmamak için).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
builder.Services.AddScoped<IStudentPhotoStorage, FileSystemStudentPhotoStorage>();
builder.Services.AddScoped<IStudentDocumentStorage, FileSystemStudentDocumentStorage>();
builder.Services.AddScoped<IAdminInstructorService, AdminInstructorService>();
builder.Services.AddScoped<IAdminTermService, AdminTermService>();
builder.Services.AddScoped<SIS.Application.AcademicCalendar.Services.IAcademicCalendarService, AcademicCalendarService>();
builder.Services.AddScoped<ICalendarTemplateService, CalendarTemplateService>();
builder.Services.AddScoped<IHolidayCalculatorService, HolidayCalculatorService>();
builder.Services.AddScoped<IAcademicCalendarRuleService, AcademicCalendarRuleService>();
builder.Services.AddScoped<ICalendarGeneratorService, CalendarGeneratorService>();
builder.Services.AddScoped<IAdminAcademicStructureService, AdminAcademicStructureService>();
builder.Services.AddScoped<IAdminCourseCatalogService, AdminCourseCatalogService>();
builder.Services.AddScoped<ISystemConfigProvider, SystemConfigProvider>();
builder.Services.AddScoped<IWebModuleSettingsProvider, WebModuleSettingsProvider>();
builder.Services.AddScoped<IWebModuleControlService, WebModuleControlService>();

// RBAC (Phase 1 + Phase 2)
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPermissionBundleService, PermissionBundleService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IMenuAdminService, MenuAdminService>();
builder.Services.AddScoped<IPermissionDelegationService, PermissionDelegationService>();
builder.Services.AddScoped<IReportAuthorizationService, ReportAuthorizationService>();
builder.Services.AddScoped<SIS.Application.Reports.IReportAccessResolver, SIS.Infrastructure.Services.Reports.ReportAccessResolver>();

// Rapor motoru (Faz 1): handler registry + tek çatı
builder.Services.AddScoped<SIS.Application.Reports.IUserRoleReader, SIS.Infrastructure.Services.Reports.UserRoleReader>();
builder.Services.AddScoped<SIS.Application.Reports.IReportExecutionService, SIS.Infrastructure.Services.Reports.ReportExecutionService>();
builder.Services.AddSingleton<SIS.Application.Reports.IReportExportService, SIS.Infrastructure.Services.Reports.ReportExportService>();
// Tüm IReportHandler implementasyonlarını otomatik kaydet (yeni rapor = sadece yeni handler sınıfı)
foreach (var handlerType in typeof(SIS.Infrastructure.Services.Reports.Handlers.StudentListReportHandler).Assembly.GetTypes()
             .Where(t => t is { IsAbstract: false, IsInterface: false }
                         && typeof(SIS.Application.Reports.IReportHandler).IsAssignableFrom(t)))
{
    builder.Services.AddScoped(typeof(SIS.Application.Reports.IReportHandler), handlerType);
}

builder.Services.AddScoped<ICurrentUserScope, CurrentUserScope>();
builder.Services.AddScoped<IScopeGuard, ScopeGuard>();
builder.Services.AddScoped<IScopeResolver, ScopeResolver>();
builder.Services.AddScoped<IUserScopeService, UserScopeService>();

// Course Evaluation + Internal Announcements
builder.Services.AddScoped<ICourseEvaluationService, CourseEvaluationService>();
builder.Services.AddScoped<IInternalAnnouncementService, InternalAnnouncementService>();
builder.Services.AddScoped<IAnnouncementCategoryService, AnnouncementCategoryService>();
builder.Services.AddScoped<IAnnouncementAttachmentService, AnnouncementAttachmentService>();
builder.Services.AddScoped<IAnnouncementSettingsProvider, AnnouncementSettingsProvider>();
builder.Services.AddSingleton<IAnnouncementHtmlSanitizer, AnnouncementHtmlSanitizer>();
builder.Services.AddSingleton<IAnnouncementAttachmentStorage, FileSystemAnnouncementAttachmentStorage>();
builder.Services.Configure<AttachmentStorageOptions>(
    builder.Configuration.GetSection(AttachmentStorageOptions.SectionName));

// Time Provider
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

var app = builder.Build();

if (kurulumModu)
{
    SIS.Infrastructure.Persistence.Kurulum.KurulumSecenekleri? secenekler = null;
    try
    {
        secenekler = SIS.Infrastructure.Persistence.Kurulum.KurulumSecenekleri.Oku(args, Environment.GetEnvironmentVariable);
    }
    catch (ArgumentException ex)
    {
        app.Logger.LogError("Kurulum başlamadı: {Ileti}", ex.Message);
        Environment.ExitCode = 1;
    }
    if (secenekler is not null)
        Environment.ExitCode = await SIS.Infrastructure.Persistence.Kurulum.KurulumCalistirici.CalistirAsync(app.Services, secenekler, app.Logger);
    await app.DisposeAsync(); // konsol günlükçüsü kuyruktaki iletileri çıkmadan önce yazsın
    return;
}

app.UseMiddleware<SecurityHeadersMiddleware>();

// Global exception handler — her iki ortamda da. Exception'ı JSON body olarak
// {error, status, type, traceId, details?} formatında döndürür ve AuditLog'a yazar.
// Eski env-koşullu UseExceptionHandler bloğu (prod-only generic title + dev'de body'siz
// plain-text stack trace) bu middleware ile değiştirildi.
app.UseMiddleware<SIS.API.Middleware.GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseMiddleware<SIS.API.Middleware.ErrorLoggingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseMiddleware<SIS.API.Middleware.PasswordRotationMiddleware>();
app.UseAuthorization();
app.UseMiddleware<SIS.API.Middleware.ScopeViolationMiddleware>();
app.UseMiddleware<SIS.API.Middleware.ScopePreloadMiddleware>();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<SIS.API.Hubs.ScheduleProgressHub>("/hubs/schedule-progress");
app.MapHub<SIS.API.Hubs.NotificationHub>("/hubs/notifications");

// Seed database
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    await SeedData.SeedAsync(context, userManager, roleManager);

    // Auto-schedule algoritma test verisi — Mühendislik fakültesinin gerçek dönem verisi
    // (Güz 2024-25 + Bahar 2025-26, 162 section, hoca atanmamış). Idempotent — varsa skip.
    // Memory: project_autoschedule_test_data.md, project_autoschedule_test_disabled_constraints.md
    await SeedData.SeedAutoScheduleTestDataAsync(context);
}

app.Run();
