using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    // Örnek hesapların ortak parolası: SIS_DEMO_PASSWORD ortam değişkeninden; yoksa her çalışmada
    // rastgele (koda yazılı parola yok; duman testleri kendi parolalarını ayrıca yazar).
    public const string DemoParolasiDegiskeni = "SIS_DEMO_PASSWORD";

    private static readonly Lazy<string> demoParolasi = new(() =>
        Environment.GetEnvironmentVariable(DemoParolasiDegiskeni) is { Length: > 0 } p
            ? p
            : "Aa1!" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)));

    private static string DemoParolasi => demoParolasi.Value;

    private static async Task SeedUsersAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        // ============================================================
        // 1. ADMIN
        // ============================================================
        await EnsureUserAsync(userManager, "yonetici@example.edu", "Örnek", "Yönetici", DemoParolasi, "SystemAdministrator");
        await EnsureUserAsync(userManager, "idari@example.edu", "Örnek", "Personel", DemoParolasi, "AdministrativeStaff");
        await EnsureUserAsync(userManager, "danisman@example.edu", "Örnek", "Danışman", DemoParolasi, "AcademicAdvisor");

        // ============================================================
        // 2. INSTRUCTORS (36 total)
        // ============================================================
        var instructorData = new (string First, string Last, string Email, string DeptCode, string Title)[]
        {
            // BM (16 instructors)
            ("Ahmet", "Çelik", "ogretim01@example.edu", "BM", "Prof. Dr."),
            ("Büşra", "Çetin", "ogretim02@example.edu", "BM", "Prof. Dr."),
            ("Süleyman", "Ateş", "ogretim03@example.edu", "BM", "Doç. Dr."),
            ("Özlem", "Karataş", "ogretim04@example.edu", "BM", "Doç. Dr."),
            ("Baran", "Gündüz", "ogretim05@example.edu", "BM", "Dr."),
            ("Melisa", "Ekinci", "ogretim06@example.edu", "BM", "Dr."),
            ("Gökhan", "Zengin", "ogretim07@example.edu", "BM", "Dr."),
            ("Yaren", "Savaş", "ogretim08@example.edu", "BM", "Dr."),
            ("Berk", "Kalkan", "ogretim09@example.edu", "BM", "Dr."),
            ("Nil", "Çoban", "ogretim10@example.edu", "BM", "Dr."),
            ("Tarık", "Sarı", "ogretim11@example.edu", "BM", "Dr."),
            ("Yeliz", "Kızıl", "ogretim12@example.edu", "BM", "Dr."),
            ("Doğukan", "Erdal", "ogretim13@example.edu", "BM", "Dr."),
            ("Miray", "Varışlı", "ogretim14@example.edu", "BM", "Arş. Gör."),
            ("Alihan", "Solmaz", "ogretim15@example.edu", "BM", "Arş. Gör."),
            ("Elif", "Öksüz", "ogretim16@example.edu", "BM", "Arş. Gör."),
            ("Ramazan", "Yıldız", "ogretim17@example.edu", "BM", "Dr."),
            // EM (10 instructors)
            ("Gül", "Koç", "ogretim18@example.edu", "EM", "Doç. Dr."),
            ("Berkay", "Çakmak", "ogretim19@example.edu", "EM", "Doç. Dr."),
            ("Gamze", "Sağlam", "ogretim20@example.edu", "EM", "Dr."),
            ("Caner", "Keskin", "ogretim21@example.edu", "EM", "Dr."),
            ("Azra", "Gökçe", "ogretim22@example.edu", "EM", "Dr."),
            ("Yiğit", "Bayram", "ogretim23@example.edu", "EM", "Dr."),
            ("Bahar", "Ulusoy", "ogretim24@example.edu", "EM", "Emeritus Prof. Dr."),
            ("Ozan", "Okumuş", "ogretim25@example.edu", "EM", "Doç. Dr."),
            ("Ülkü", "Elmas", "ogretim26@example.edu", "EM", "Dr."),
            ("Eray", "Uzun", "ogretim27@example.edu", "EM", "Arş. Gör."),
            // EEM (9 instructors)
            ("Kardelen", "Öğüt", "ogretim28@example.edu", "EEM", "Prof. Dr."),
            ("Vedat", "Göker", "ogretim29@example.edu", "EEM", "Doç. Dr."),
            ("Hatice", "Aksu", "ogretim30@example.edu", "EEM", "Dr."),
            ("Murat", "Yalın", "ogretim31@example.edu", "EEM", "Dr."),
            ("Derya", "Tosun", "ogretim32@example.edu", "EEM", "Dr."),
            ("Enes", "Öztürk", "ogretim33@example.edu", "EEM", "Dr."),
            ("Burcu", "Özkan", "ogretim34@example.edu", "EEM", "Dr."),
            ("Selim", "Kaplan", "ogretim35@example.edu", "EEM", "Dr."),
            ("Sude", "Avcı", "ogretim36@example.edu", "EEM", "Arş. Gör."),
        };

        var depts = context.Departments.ToDictionary(d => d.Code, d => d.Id);
        foreach (var (first, last, email, deptCode, title) in instructorData)
        {
            var user = await EnsureUserAsync(userManager, email, first, last, DemoParolasi, "Instructor");
            if (!context.Instructors.Any(i => i.UserId == user.Id))
            {
                context.Instructors.Add(new Instructor
                {
                    UserId = user.Id,
                    DepartmentId = depts[deptCode],
                    Title = title,
                    MaxWeeklyHours = ResolveMaxWeeklyHours(title)
                });
            }
        }
        await context.SaveChangesAsync();

        // Department Managers (also have Instructor role from above)
        await EnsureUserAsync(userManager, "ogretim01@example.edu", "Ahmet", "Çelik", DemoParolasi, "DepartmentManager");
        await EnsureUserAsync(userManager, "ogretim02@example.edu", "Büşra", "Çetin", DemoParolasi, "DepartmentManager");
        await EnsureUserAsync(userManager, "ogretim04@example.edu", "Özlem", "Karataş", DemoParolasi, "DepartmentManager");
        await EnsureUserAsync(userManager, "ogretim18@example.edu", "Gül", "Koç", DemoParolasi, "DepartmentManager");

        // Dean (also has Instructor role from above — faculty-level authority)
        var deanUser = await EnsureUserAsync(userManager, "ogretim28@example.edu", "Kardelen", "Öğüt", DemoParolasi, "Dean");
        await EnsureDeanScopeAsync(context, deanUser);

        // ============================================================
        // 3. STUDENTS (1000 total)
        // ============================================================
        // Original 18 students — names, emails, student numbers preserved
        var existingStudents = new (string First, string Last, string Email, string Num, string NatId,
            string ProgCode, decimal Cgpa, int Semesters, bool Scholar, int Year)[]
        {
            ("Mehmet", "Öztürk", "ornek01@example.edu", "2201001", "1220100100000", "BM-L", 3.20m, 7, true, 2022),
            ("Merve", "Polat", "ornek02@example.edu", "2401002", "1240100200000", "BM-L", 1.85m, 3, false, 2024),
            ("Ömer", "Bayrak", "ornek03@example.edu", "2501003", "1250100300000", "BM-L", 1.40m, 1, false, 2025),
            ("Hülya", "Erdem", "ornek04@example.edu", "2302001", "1230200100000", "BMTR-L", 3.10m, 5, false, 2023),
            ("Serkan", "Fırat", "ornek05@example.edu", "2402002", "1240200200000", "BMTR-L", 1.90m, 3, false, 2024),
            ("Selin", "Çakır", "ornek06@example.edu", "2202003", "1220200300000", "BMTR-L", 1.50m, 7, false, 2022),
            ("Deniz", "Altın", "ornek07@example.edu", "2203001", "1220300100000", "BSM-L", 3.50m, 7, false, 2022),
            ("Başak", "Yazıcı", "ornek08@example.edu", "2303002", "1230300200000", "BSM-L", 1.80m, 5, false, 2023),
            ("Uğur", "Oğuz", "ornek09@example.edu", "2403003", "1240300300000", "BSM-L", 1.45m, 3, false, 2024),
            ("Nehir", "Kızıl", "ornek10@example.edu", "2204001", "1220400100000", "EM-L", 2.80m, 7, false, 2022),
            ("Doruk", "Göker", "ornek11@example.edu", "2504002", "1250400200000", "EM-L", 1.75m, 1, false, 2025),
            ("Serra", "Candemir", "ornek12@example.edu", "2304003", "1230400300000", "EM-L", 1.55m, 5, false, 2023),
            ("Görkem", "Dursun", "ornek13@example.edu", "2205001", "1220500100000", "EEM-L", 3.30m, 7, false, 2022),
            ("Ilgın", "Demir", "ornek14@example.edu", "2405002", "1240500200000", "EEM-L", 1.95m, 3, false, 2024),
            ("Bilal", "Kara", "ornek15@example.edu", "2305003", "1230500300000", "EEM-L", 1.60m, 5, false, 2023),
            ("Vildan", "Aksoy", "ornek16@example.edu", "2306001", "1230600100000", "YM-L", 2.70m, 5, false, 2023),
            ("Yunus", "Serdar", "ornek17@example.edu", "2506002", "1250600200000", "YM-L", 1.85m, 1, false, 2025),
            ("Hande", "Alkan", "ornek18@example.edu", "2406003", "1240600300000", "YM-L", 1.50m, 3, false, 2024),
        };

        var existingByProgram = existingStudents
            .GroupBy(s => s.ProgCode)
            .ToDictionary(g => g.Key, g => g.ToList());

        var programs = context.AcademicPrograms.Include(p => p.Department).ToDictionary(p => p.Code);
        var curricula = context.Curricula
            .Where(c => c.IsActive)
            .GroupBy(c => c.ProgramId)
            .ToDictionary(g => g.Key, g => g.First());
        var instructorsByDept = context.Instructors.ToList()
            .GroupBy(i => i.DepartmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var programDistribution = new (string ProgCode, int Count)[]
        {
            ("BM-L", 250), ("BSM-L", 200), ("BMTR-L", 150),
            ("EM-L", 150), ("EEM-L", 130), ("YM-L", 120)
        };

        var semesterGroups = new (int Low, int High, double Ratio)[]
        {
            (1, 2, 0.30), (3, 4, 0.28), (5, 6, 0.24), (7, 8, 0.18)
        };

        var rng = new Random(42);
        int globalStudentIndex = 0;

        foreach (var (progCode, totalCount) in programDistribution)
        {
            var prog = programs[progCode];
            var curr = curricula[prog.Id];
            var deptInstructors = instructorsByDept.GetValueOrDefault(prog.DepartmentId, []);
            if (deptInstructors.Count == 0)
                deptInstructors = context.Instructors.Take(1).ToList();

            int advisorIdx = 0;
            int progSeqNo = 0;

            var existing = existingByProgram.GetValueOrDefault(progCode, []);
            int existingUsed = 0;

            // Calculate group counts with rounding adjustment
            var groupCounts = semesterGroups.Select(g => (int)Math.Round(totalCount * g.Ratio)).ToArray();
            int diff = totalCount - groupCounts.Sum();
            groupCounts[0] += diff; // adjust first group to hit exact total

            for (int gi = 0; gi < semesterGroups.Length; gi++)
            {
                var (semLow, semHigh, _) = semesterGroups[gi];
                int groupCount = groupCounts[gi];
                int halfCount = groupCount / 2;

                for (int i = 0; i < groupCount; i++)
                {
                    // 50/50 odd/even split, extra goes to even
                    int completedSemesters = i < halfCount ? semLow : semHigh;
                    int entryYear = completedSemesters switch
                    {
                        >= 7 => 2022,
                        >= 5 => 2023,
                        >= 3 => 2024,
                        _ => 2025
                    };

                    // Bell-curve CGPA from latent ability (θ ~ N(0,1)); see SeedData.GradeDistribution.
                    // This provisional value seeds the student's ability — SeedRegistrationsAsync
                    // recomputes the real CGPA from generated grades. It is also the display
                    // fallback for students who have no completed terms yet.
                    var cgpa = AbilityToCgpa(NextNormal(rng));

                    string firstName, lastName, email, studentNum, nationalId;
                    bool isScholar;

                    if (existingUsed < existing.Count)
                    {
                        var ex = existing[existingUsed];
                        firstName = ex.First;
                        lastName = ex.Last;
                        email = ex.Email;
                        studentNum = ex.Num;
                        nationalId = ex.NatId;
                        isScholar = ex.Scholar;
                        cgpa = ex.Cgpa; // preserve original for pattern selection
                        completedSemesters = ex.Semesters;
                        entryYear = ex.Year;
                        existingUsed++;
                    }
                    else
                    {
                        progSeqNo++;
                        bool isFemale = rng.Next(2) == 0;
                        firstName = isFemale
                            ? FemaleFirstNames[rng.Next(FemaleFirstNames.Length)]
                            : MaleFirstNames[rng.Next(MaleFirstNames.Length)];
                        lastName = Surnames[rng.Next(Surnames.Length)];
                        studentNum = $"{entryYear % 100:D2}{Array.IndexOf(programDistribution.Select(p => p.ProgCode).ToArray(), progCode):D1}{progSeqNo:D4}";
                        nationalId = $"{1000000000000 + globalStudentIndex + 18}";
                        email = $"ogrenci{globalStudentIndex + 1:D4}@example.edu";
                        isScholar = globalStudentIndex % 10 == 0;
                    }

                    globalStudentIndex++;

                    var user = await EnsureUserAsync(userManager, email, firstName, lastName, DemoParolasi, "Student");
                    if (!context.Students.Any(st => st.UserId == user.Id || st.NationalId == nationalId || st.StudentNumber == studentNum))
                    {
                        var advisor = deptInstructors[advisorIdx % deptInstructors.Count];
                        advisorIdx++;

                        var student = new Student
                        {
                            UserId = user.Id,
                            StudentNumber = studentNum,
                            NationalId = nationalId,
                            Status = StudentStatus.Active,
                            Cgpa = cgpa,
                            Sgpa = cgpa,
                            CompletedSemesters = completedSemesters,
                            FrozenSemesterCount = 0,
                            WithdrawalCount = 0,
                            IsScholarshipStudent = isScholar,
                            HasFinancialBlock = false,
                            AdvisorId = advisor.Id
                        };
                        context.Students.Add(student);
                        await context.SaveChangesAsync();

                        context.Set<StudentProgram>().Add(new StudentProgram
                        {
                            StudentId = student.Id,
                            ProgramId = prog.Id,
                            CurriculumId = curr.Id,
                            Type = StudentProgramType.Main,
                            EnrollmentDate = new DateTime(entryYear, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                            IsActive = true
                        });
                        await context.SaveChangesAsync();
                    }

                    // İzlenen nesne sayısı büyüdükçe her kayıt yavaşlıyordu (1000 öğrenci); kaydedilenler bırakılır.
                    context.ChangeTracker.Clear();
                }
            }
        }
    }

    // Aynı parolanın özeti bir kez hesaplanır (parola kuralları o ilk kayıtta denetlenir), sonraki
    // hesaplar aynı özetle oluşturulur: özet tuzunu içinde taşır, kullanıcıya bağlı değildir.
    private static readonly Dictionary<string, string> parolaOzetleri = [];

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email, string firstName, string lastName,
        string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                PasswordChangedAtUtc = DateTime.UtcNow
            };
            IdentityResult result;
            if (parolaOzetleri.TryGetValue(password, out var ozet))
            {
                user.PasswordHash = ozet;
                result = await userManager.CreateAsync(user);
            }
            else
            {
                result = await userManager.CreateAsync(user, password);
                if (result.Succeeded) parolaOzetleri[password] = user.PasswordHash!;
            }
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        else if (user.PasswordChangedAtUtc == null)
        {
            user.PasswordChangedAtUtc = DateTime.UtcNow;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to update user '{email}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var addRoleResult = await userManager.AddToRoleAsync(user, role);
            if (!addRoleResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to assign role '{role}' to '{email}': {string.Join(", ", addRoleResult.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    private static int ResolveMaxWeeklyHours(string title) => title switch
    {
        var t when t.StartsWith("Prof.") => 10,
        var t when t.StartsWith("Doç.") => 12,
        var t when t.StartsWith("Dr. Öğr.") || t.StartsWith("Dr.") => 12,
        var t when t.StartsWith("Öğr. Gör.") => 16,
        var t when t.Contains("Prof.") => 10, // "Emeritus Prof. Dr." etc.
        _ => 20 // Arş. Gör. and others
    };

    private static async Task EnsureDeanScopeAsync(AppDbContext context, ApplicationUser deanUser)
    {
        var existingScope = await context.UserScopes
            .FirstOrDefaultAsync(scope => scope.UserId == deanUser.Id && scope.Kind == SIS.Domain.Identity.ScopeKind.Faculty);
        if (existingScope is not null)
            return;

        var facultyId = await context.Instructors
            .Where(instructor => instructor.UserId == deanUser.Id)
            .Select(instructor => (int?)instructor.Department.FacultyId)
            .FirstOrDefaultAsync();
        if (facultyId is null)
            return;

        context.UserScopes.Add(new SIS.Domain.Identity.UserScope
        {
            UserId = deanUser.Id,
            Kind = SIS.Domain.Identity.ScopeKind.Faculty,
            FacultyId = facultyId.Value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }
}
