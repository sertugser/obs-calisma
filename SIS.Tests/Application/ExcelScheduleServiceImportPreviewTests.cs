using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

// Önizleme, var olmayan fakülte ya da dönem için oturum yazmaz (InMemory'de yabancı anahtar denetimi yok).
public class ExcelScheduleServiceImportPreviewTests
{
    private const int UserId = 10;
    private const int FacultyId = 7;
    private const int TermId = 12;
    private const int MissingId = 99;

    [Fact]
    public async Task PreviewImportAsync_ExistingFacultyAndTerm_WritesSession()
    {
        var database = await SeedAsync();

        await using (var context = CreateContext(database))
        {
            using var file = HeaderOnlyWorkbook();
            await new ExcelScheduleService(context).PreviewImportAsync(UserId, file, TermId, FacultyId);
        }

        Assert.Equal(new[] { (FacultyId, TermId) }, await SessionsAsync(database));
    }

    [Fact]
    public async Task PreviewImportAsync_UnknownFaculty_ThrowsWithoutWritingSession()
    {
        var database = await SeedAsync();

        await using (var context = CreateContext(database))
        {
            using var file = HeaderOnlyWorkbook();
            var ex = await Assert.ThrowsAsync<ScheduleImportSessionInvalidException>(
                () => new ExcelScheduleService(context).PreviewImportAsync(UserId, file, TermId, MissingId));
            Assert.Equal("Fakülte bulunamadı.", ex.Message);
        }

        Assert.Empty(await SessionsAsync(database));
    }

    [Fact]
    public async Task PreviewImportAsync_UnknownTerm_ThrowsWithoutWritingSession()
    {
        var database = await SeedAsync();

        await using (var context = CreateContext(database))
        {
            using var file = HeaderOnlyWorkbook();
            var ex = await Assert.ThrowsAsync<ScheduleImportSessionInvalidException>(
                () => new ExcelScheduleService(context).PreviewImportAsync(UserId, file, MissingId, FacultyId));
            Assert.Equal("Dönem bulunamadı.", ex.Message);
        }

        Assert.Empty(await SessionsAsync(database));
    }

    private static AppDbContext CreateContext(string database) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(database).Options);

    private static async Task<string> SeedAsync()
    {
        var database = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        await using var context = CreateContext(database);
        context.Faculties.Add(new Faculty { Id = FacultyId, Code = "ENG", Name = "Engineering", CreatedAt = now });
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2026 Spring",
            ShortName = "26S",
            Year = 2026,
            TermStart = now.AddDays(-30),
            TermEnd = now.AddDays(90),
            CreatedAt = now,
        });
        await context.SaveChangesAsync();
        return database;
    }

    // Yazılan oturumların fakülte ve dönemi, ayrı bir context'le.
    private static async Task<(int, int)[]> SessionsAsync(string database)
    {
        await using var context = CreateContext(database);
        var sessions = await context.ScheduleImportSessions.Select(s => new { s.FacultyId, s.TermId }).ToListAsync();
        return sessions.Select(s => (s.FacultyId, s.TermId)).ToArray();
    }

    private static MemoryStream HeaderOnlyWorkbook()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Program Verisi").Cell(1, 1).Value = "Ders Kodu";
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
