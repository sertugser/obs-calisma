using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedAcademicStructureAsync(AppDbContext context)
    {
        // Faculty
        if (!context.Faculties.Any())
        {
            context.Faculties.Add(new Faculty { Name = "Mühendislik Fakültesi", Code = "MUH" });
            await context.SaveChangesAsync();
        }
        var faculty = context.Faculties.First(f => f.Code == "MUH");

        // Departments
        var deptData = new (string Name, string Code)[]
        {
            ("Bilgisayar Mühendisliği", "BM"),
            ("Bilgisayar Mühendisliği (Türkçe)", "BMTR"),
            ("Bilişim Sistemleri Mühendisliği", "BSM"),
            ("Endüstri Mühendisliği", "EM"),
            ("Elektrik Elektronik Mühendisliği", "EEM"),
            ("Yazılım Mühendisliği", "YM"),
        };
        foreach (var (name, code) in deptData)
        {
            if (!context.Departments.Any(d => d.Code == code))
            {
                context.Departments.Add(new Department { Name = name, Code = code, FacultyId = faculty.Id });
            }
        }
        await context.SaveChangesAsync();

        // Madde 11: Koordinatörlük (Service Unit) seed — ortak derslerin sahibi
        // MATH, FIZ, KIM, AIIT, TRD, YDL gibi ortak dersler bu birime atanır.
        var coordinatorships = new (string Name, string Code)[]
        {
            ("Temel Bilimler Koordinatörlüğü", "TBK"),
            ("Atatürk İlkeleri ve İnkılap Tarihi Bölümü", "AIIT"),
            ("Türk Dili Bölümü", "TRD"),
            ("Yabancı Diller Yüksekokulu", "YDL"),
        };
        foreach (var (name, code) in coordinatorships)
        {
            if (!context.Departments.Any(d => d.Code == code))
            {
                context.Departments.Add(new Department
                {
                    Name = name,
                    Code = code,
                    FacultyId = faculty.Id,
                    IsServiceUnit = true,
                });
            }
        }
        await context.SaveChangesAsync();

        // Programs
        var progData = new (string Name, string Code, string DeptCode)[]
        {
            ("Bilgisayar Mühendisliği Lisans", "BM-L", "BM"),
            ("Bilgisayar Mühendisliği (Türkçe) Lisans", "BMTR-L", "BMTR"),
            ("Bilisim Sistemleri Muhendisligi Lisans", "BSM-L", "BSM"),
            ("Endüstri Mühendisliği Lisans", "EM-L", "EM"),
            ("Elektrik Elektronik Mühendisliği Lisans", "EEM-L", "EEM"),
            ("Yazılım Mühendisliği Lisans", "YM-L", "YM"),
        };
        foreach (var (name, code, deptCode) in progData)
        {
            if (!context.AcademicPrograms.Any(p => p.Code == code))
            {
                var dept = context.Departments.First(d => d.Code == deptCode);
                context.AcademicPrograms.Add(new AcademicProgram
                {
                    Name = name,
                    Code = code,
                    DepartmentId = dept.Id,
                    DegreeLevel = DegreeLevel.Bachelor,
                    EducationType = EducationType.Semester,
                    MaxStudyDurationSemesters = 14,
                    TotalRequiredEcts = 240
                });
            }
        }
        await context.SaveChangesAsync();

        // Curricula (one per program)
        var currData = new (string ProgCode, string Version)[]
        {
            ("BM-L", "2024"), ("BMTR-L", "2024"), ("BSM-L", "2024"),
            ("EM-L", "2023"), ("EEM-L", "2024"), ("YM-L", "2024"),
        };
        foreach (var (progCode, version) in currData)
        {
            var prog = context.AcademicPrograms.First(p => p.Code == progCode);
            if (!context.Curricula.Any(c => c.ProgramId == prog.Id))
            {
                context.Curricula.Add(new Curriculum
                {
                    ProgramId = prog.Id,
                    Version = version,
                    IsActive = true
                });
            }
        }
        await context.SaveChangesAsync();
    }
}
