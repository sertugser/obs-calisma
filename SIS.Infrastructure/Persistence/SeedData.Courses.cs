using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    internal static async Task SeedCoursesAsync(AppDbContext context)
    {
        // Madde 10: Null DepartmentId olan dersleri Temel Bilimler Koordinatörlüğü'ne ata
        // (Ortak dersler artık sahipsiz kalmıyor — NOT NULL guard'ı öncesi tek seferlik temizlik)
        var tbk = await context.Departments.FirstOrDefaultAsync(d => d.Code == "TBK");
        if (tbk != null)
        {
            var orphanCourses = await context.Courses.Where(c => c.DepartmentId == null).ToListAsync();
            foreach (var c in orphanCourses)
                c.DepartmentId = tbk.Id;
            if (orphanCourses.Count > 0)
                await context.SaveChangesAsync();
        }

        // ============================================================
        // 1. COURSES (213 total)
        // ============================================================
        if (!context.Courses.Any())
        {
            // (code, name, deptCode, ects, theory, lab, isNonGpa)
            // deptCode: null = shared/no department
            var courseData = new (string Code, string Name, string? Dept, int Ects, int Th, int Lab, bool Ng)[]
            {
                // ── BM (Dept: BM) ──
                ("CENG101", "Algorithms and Programming with Java I", "BM", 6, 3, 2, false),
                ("OHS101", "Occupational Health and Safety I", "BM", 1, 1, 0, true),
                ("ENG101", "Academic English I", "BM", 2, 2, 0, true),
                ("MATH101", "Calculus I", "BM", 6, 4, 0, false),
                ("PHY101", "Physics I", "BM", 5, 3, 0, false),
                ("PHY103", "Physics I Lab", "BM", 1, 0, 2, false),
                ("BIO101", "Molecular Biology and Biochemistry", "BM", 5, 3, 0, false),
                ("HIS101", "Principles of Ataturk and History of Revolutions I", "BM", 2, 2, 0, true),
                ("TUR101", "Turkish I", "BM", 2, 2, 0, true),
                ("CENG102", "Algorithms and Programming with Java II", "BM", 8, 3, 4, false),
                ("OHS102", "Occupational Health and Safety II", "BM", 1, 1, 0, true),
                ("ENG102", "Academic English II", "BM", 2, 2, 0, true),
                ("MATH102", "Calculus II", "BM", 6, 4, 0, false),
                ("PHY102", "Physics II", "BM", 5, 3, 0, false),
                ("PHY104", "Physics II Lab", "BM", 1, 0, 2, false),
                ("HIS102", "Principles of Ataturk and History of Revolutions II", "BM", 2, 2, 0, true),
                ("TUR102", "Turkish II", "BM", 2, 2, 0, true),
                ("EEE203", "Digital Design", "BM", 5, 3, 2, false),
                ("EEE281", "Electrical Circuits for Comp. Eng.", "BM", 5, 3, 2, false),
                ("CENG213", "Data Structures and Algorithms", "BM", 5, 3, 0, false),
                ("CENG223", "Discrete and Combinatorial Math.", "BM", 5, 3, 0, false),
                ("CENG241", "Programming Languages", "BM", 6, 3, 0, false),
                ("ENG201", "Academic English III", "BM", 2, 2, 0, true),
                ("CENG214", "Algorithms II", "BM", 6, 3, 0, false),
                ("CENG256", "Formal Languages and Automata Theory", "BM", 6, 3, 0, false),
                ("MATH224", "Linear Algebra and Differential Equations", "BM", 5, 3, 0, false),
                ("MATH206", "Probability Theory and Statistics", "BM", 5, 3, 0, false),
                ("SENG264", "Intro. to AI and Machine Learning", "BM", 6, 3, 0, false),
                ("CENG301", "Operating Systems", "BM", 6, 3, 0, false),
                ("CENG351", "Database Management Systems", "BM", 6, 3, 0, false),
                ("SENG321", "Software Engineering", "BM", 6, 3, 0, false),
                ("CENG491", "Graduation Project I", "BM", 5, 3, 2, false),
                ("CENG492", "Graduation Project II", "BM", 5, 3, 2, false),
                ("CENG332", "Computer Architecture", "BM", 5, 3, 2, false),
                ("CENG490", "Long Term Practice", "BM", 25, 0, 16, false),
                ("CENG375", "Machine Learning", "BM", 5, 3, 0, false),

                // ── BMTR (Dept: BMTR) ──
                ("BM101", "Java ile Algoritmalar ve Programlama I", "BMTR", 6, 3, 2, false),
                ("BM102", "Algoritmalar ve Java ile Programlama II", "BMTR", 8, 3, 4, false),
                ("BM203", "Dijital Tasarım", "BMTR", 5, 3, 2, false),
                ("BM281", "Bilgisayar Mühendisleri için Elektrik Devreleri", "BMTR", 5, 3, 2, false),
                ("BM213", "Veri Yapıları ve Algoritmalar", "BMTR", 5, 3, 0, false),
                ("BM223", "Ayrık ve Kombinatoryal Matematik", "BMTR", 5, 3, 0, false),
                ("BM241", "Programlama Dilleri", "BMTR", 6, 3, 0, false),
                ("BM214", "Algoritmalar II", "BMTR", 6, 3, 0, false),
                ("BM256", "Biçimsel Diller ve Otomata Teorisi", "BMTR", 6, 3, 0, false),
                ("BM264", "Yapay Zeka ve Makine Öğrenimine Giriş", "BMTR", 6, 3, 0, false),
                ("BM301", "İşletim Sistemleri", "BMTR", 6, 3, 0, false),
                ("BM351", "Veritabanı Yönetim Sistemleri", "BMTR", 6, 3, 0, false),
                ("BM321", "Yazılım Mühendisliği", "BMTR", 6, 3, 0, false),
                ("BM361", "Web Tasarımı ve Programlama", "BMTR", 5, 3, 0, false),
                ("BM332", "Bilgisayar Mimarisi", "BMTR", 5, 3, 2, false),
                ("BM336", "Bilgisayar Ağları", "BMTR", 6, 3, 0, false),
                ("BM324", "Yazılım Tasarım Örüntüleri", "BMTR", 6, 3, 0, false),
                ("BM384", "Ayrık Sinyal İşlemeye Giriş", "BMTR", 5, 3, 0, false),
                ("BM491", "Mezuniyet Projesi I", "BMTR", 5, 3, 2, false),
                ("BM492", "Mezuniyet Projesi II", "BMTR", 5, 3, 2, false),
                ("BM490", "Uzun Süreli Uygulama", "BMTR", 25, 0, 16, false),

                // ── Shared / No department ──
                ("ISG101", "İş Sağlığı ve Güvenliği I", null, 1, 1, 0, false),
                ("ING101", "Akademik İngilizce I", null, 2, 2, 0, false),
                ("MAT101", "Matematik I", null, 6, 4, 0, false),
                ("FIZ101", "Fizik I", null, 5, 3, 0, false),
                ("FIZ103", "Fizik I Lab", null, 1, 0, 2, false),
                ("BIY101", "Moleküler Biyoloji ve Biyokimya", null, 5, 3, 0, false),
                ("ISG102", "İş Sağlığı ve Güvenliği II", null, 1, 1, 0, false),
                ("ING102", "Akademik İngilizce II", null, 2, 2, 0, false),
                ("MAT102", "Matematik II", null, 6, 4, 0, false),
                ("FIZ102", "Fizik II", null, 5, 3, 0, false),
                ("FIZ104", "Fizik II Lab", null, 1, 0, 2, false),
                ("ING201", "Akademik İngilizce III", null, 2, 2, 0, false),
                ("MAT224", "Doğrusal Cebir ve Diferansiyel Denklemler", null, 5, 3, 0, false),
                ("MAT206", "Olasılık Teorisi ve İstatistik", null, 5, 3, 0, false),
                ("ING202", "Akademik İngilizce IV", null, 2, 2, 0, false),
                ("ING301", "Akademik İngilizce V", null, 2, 2, 0, false),
                ("ING302", "Akademik İngilizce VI", null, 2, 2, 0, false),
                ("EYG401", "Eleştirel Düşünme, Yaratıcılık ve Girişimcilik", null, 2, 2, 0, false),
                ("MATH204", "Linear Algebra & Differential Equations", null, 6, 3, 0, false),
                ("ENG202", "Academic English IV", null, 2, 2, 0, false),
                ("ENG301", "Academic English V", null, 2, 2, 0, false),
                ("ENG302", "Academic English VI", null, 2, 2, 0, false),
                ("CCE401", "Critical Thinking, Creativity and Entrepreneurship", null, 2, 2, 0, false),
                ("MATH213", "Linear Algebra", null, 5, 3, 0, false),
                ("MATH215", "Differential Equations", null, 5, 3, 0, false),
                ("MATH201", "Vector and Complex Calculus", null, 6, 4, 0, false),
                ("AEE301", "Academic English V", null, 2, 2, 0, false),

                // ── BSM (Dept: BSM) ──
                ("CES101", "Introduction to Programming with Java I", "BSM", 6, 3, 2, false),
                ("ISE211", "IT Systems and Security", "BSM", 5, 3, 0, false),
                ("ISE203", "Digital Design", "BSM", 5, 3, 0, false),
                ("CES213", "Data Structures and Algorithms", "BSM", 5, 3, 0, false),
                ("SENG231", "Data Science with Python", "BSM", 5, 3, 0, false),
                ("ISE222", "Data Intelligence And Decision Support Systems", "BSM", 6, 3, 0, false),
                ("CENG353", "Database Management Systems", "BSM", 6, 3, 0, false),
                ("CENG361", "Web Design and Programming", "BSM", 5, 3, 0, false),
                ("CENG336", "Computer Networks", "BSM", 6, 3, 0, false),
                ("ISE322", "Business Process Support", "BSM", 6, 3, 0, false),
                ("ISE332", "Human Computer Interface", "BSM", 6, 3, 0, false),
                ("ISE350", "Data Warehousing and Mining", "BSM", 5, 3, 0, false),
                ("ISE491", "Graduation Project I", "BSM", 5, 3, 0, false),
                ("ISE492", "Graduation Project II", "BSM", 5, 2, 2, false),
                ("ISE490", "Internship Practice", "BSM", 25, 0, 18, false),
                ("ISE361", "Internet of Things", "BSM", 5, 3, 0, false),

                // ── EM (Dept: EM) ──
                ("IE101", "Introduction to Industrial Engineering", "EM", 5, 3, 0, false),
                ("IE112", "Computer Aided Technical Drawing", "EM", 3, 1, 2, false),
                ("IE241", "Engineering Economy", "EM", 5, 3, 0, false),
                ("IE227", "Probability Theory", "EM", 5, 3, 0, false),
                ("IE223", "Materials Science", "EM", 5, 3, 0, false),
                ("IE202", "Work Study and Ergonomics", "EM", 5, 3, 0, false),
                ("IE224", "Manufacturing Processes", "EM", 5, 3, 0, false),
                ("IE228", "Engineering Statistics", "EM", 5, 3, 0, false),
                ("IE232", "Operations Research I", "EM", 5, 3, 0, false),
                ("IE242", "Cost Analysis in Engineering", "EM", 5, 3, 0, false),
                ("IE301", "Facilities Planning", "EM", 5, 3, 0, false),
                ("IE333", "Operations Research II", "EM", 5, 3, 0, false),
                ("IE327", "System Simulation", "EM", 5, 3, 0, false),
                ("IE365", "Production Planning and Control I", "EM", 5, 3, 0, false),
                ("IE325", "Quality Engineering", "EM", 5, 3, 0, false),
                ("IE324", "Project Management", "EM", 3, 3, 0, false),
                ("IE366", "Production Planning and Control II", "EM", 5, 3, 0, false),
                ("IE346", "Decision Analysis", "EM", 5, 3, 0, false),
                ("IE491", "Graduation Project I", "EM", 5, 3, 2, false),
                ("IE489", "Seminars in IE Practice", "EM", 3, 2, 0, false),
                ("IE492", "Graduation Project II", "EM", 5, 3, 2, false),
                ("IE490", "Work Place Training", "EM", 25, 0, 16, false),
                // EM Electives
                ("IE352", "Technology Management and Innovation", "EM", 5, 3, 0, false),
                ("IE354", "Time Series Analysis and Forecasting", "EM", 5, 3, 0, false),
                ("IE356", "Risk Management", "EM", 5, 3, 0, false),
                ("IE358", "Scheduling", "EM", 5, 3, 0, false),
                ("IE362", "Supply Chain Management", "EM", 5, 3, 0, false),
                ("IE364", "Logistics", "EM", 5, 3, 0, false),
                ("IE372", "Statistical Process Control", "EM", 5, 3, 0, false),
                ("IE374", "Game Theory", "EM", 5, 3, 0, false),
                ("IE376", "Management Information Systems", "EM", 5, 3, 0, false),
                ("IE380", "Fundamentals of Business and Management", "EM", 5, 3, 0, false),
                ("IE382", "Business Communication", "EM", 5, 3, 0, false),
                ("IE384", "Organizational Behavior", "EM", 5, 3, 0, false),
                ("IE386", "Customer Relationship Management", "EM", 5, 3, 0, false),
                ("IE388", "Introduction to Sustainability", "EM", 5, 3, 0, false),
                ("IE451", "Data Science", "EM", 5, 3, 0, false),
                ("IE453", "Applied Data Analysis", "EM", 5, 3, 0, false),
                ("IE455", "Statistics with R", "EM", 5, 3, 0, false),
                ("IE457", "Design and Analysis of Experiments", "EM", 5, 3, 0, false),
                ("IE461", "Strategic Management and Planning", "EM", 5, 3, 0, false),
                ("IE463", "Decision Support Systems", "EM", 5, 3, 0, false),
                ("IE465", "Human Resources Management", "EM", 5, 3, 0, false),
                ("IE467", "Business Intelligence and Analytics", "EM", 5, 3, 0, false),
                ("IE469", "Computer Programming in IE", "EM", 5, 3, 0, false),
                ("IE471", "Applied Operations Research", "EM", 5, 3, 0, false),
                ("IE473", "Stochastic Models", "EM", 5, 3, 0, false),
                ("IE475", "System Dynamics Modeling", "EM", 5, 3, 0, false),
                ("IE477", "Pricing and Revenue Optimization", "EM", 5, 3, 0, false),
                ("IE479", "Flexible Production Systems", "EM", 5, 3, 0, false),
                ("IE481", "Quality Management Systems", "EM", 5, 3, 0, false),

                // ── EEM (Dept: EEM) ──
                ("EEE102", "Introduction to EEE", "EEM", 3, 2, 0, false),
                ("EEE201", "Circuit Theory I", "EEM", 6, 4, 0, false),
                ("EEE205", "Computer Tools for Electrical Engineering", "EEM", 3, 2, 1, false),
                ("EEE202", "Circuit Theory II", "EEM", 6, 4, 0, false),
                ("EEE208", "Signals and Systems", "EEM", 5, 3, 0, false),
                ("EEE236", "Electromagnetic Field Theory", "EEM", 5, 3, 0, false),
                ("EEE204", "Electronics I", "EEM", 5, 3, 0, false),
                ("EEE212", "Circuit Theory Lab", "EEM", 2, 0, 2, false),
                ("EEE301", "Telecommunications I", "EEM", 6, 3, 2, false),
                ("EEE303", "Electromagnetics Wave Theory", "EEM", 5, 3, 0, false),
                ("EEE313", "Electronics II", "EEM", 5, 3, 0, false),
                ("EEE331", "Electrical Machines", "EEM", 5, 3, 2, false),
                ("EEE321", "Digital Signal Processing", "EEM", 5, 3, 0, false),
                ("EEE315", "Electronics Lab", "EEM", 2, 0, 2, false),
                ("EEE302", "Telecommunications II", "EEM", 5, 3, 2, false),
                ("EEE304", "Control Theory", "EEM", 5, 3, 2, false),
                ("EEE306", "Microprocessors", "EEM", 5, 3, 2, false),
                ("EEE491", "Graduation Project I", "EEM", 5, 2, 2, false),
                ("EEE492", "Graduation Project II", "EEM", 5, 3, 2, false),
                ("EEE490", "Long-Term Practice", "EEM", 25, 0, 25, false),
                // EEM Electives
                ("EEE318", "Matlab Applications in Electrical Engineering", "EEM", 5, 3, 0, false),
                ("EEE320", "Neural Networks", "EEM", 5, 3, 0, false),
                ("EEE312", "Utilization of Electrical Energy", "EEM", 5, 3, 0, false),
                ("EEE322", "Introduction to Power Systems", "EEM", 5, 3, 0, false),
                ("EEE324", "Microwave Electronics", "EEM", 5, 3, 0, false),
                ("EEE326", "CMOS VLSI Design, HDL", "EEM", 5, 3, 0, false),
                ("EEE328", "Introduction to Robotics", "EEM", 5, 3, 0, false),
                ("EEE330", "Electrical & Electronics Measurements", "EEM", 5, 3, 0, false),
                ("EEE332", "Electrical Energy Transmission and Distribution", "EEM", 5, 3, 0, false),
                ("EEE334", "Introduction to Machine Learning", "EEM", 5, 3, 0, false),
                ("EEE338", "Optical Fiber Communications", "EEM", 5, 3, 0, false),
                ("EEE423", "Antennas and Propagation", "EEM", 5, 3, 3, false),
                ("EEE455", "Wireless Networking Technologies and Applications", "EEM", 5, 3, 3, false),
                ("EEE421", "Digital Image Processing", "EEM", 5, 3, 3, false),
                ("EEE425", "Process Control", "EEM", 5, 3, 3, false),
                ("EEE468", "Numerical Methods in EE", "EEM", 5, 3, 3, false),
                ("EEE471", "High Voltage Techniques", "EEM", 5, 3, 3, false),
                ("EEE431", "Power System Analysis", "EEM", 5, 3, 3, false),
                ("EEE433", "Artificial Intelligence", "EEM", 5, 3, 3, false),
                ("EEE477", "Mobile Communications", "EEM", 5, 3, 3, false),
                ("EEE435", "Power System Protection", "EEM", 5, 3, 3, false),
                ("EEE467", "Renewable Energy", "EEM", 5, 3, 3, false),
                ("EEE484", "Power Electronics", "EEM", 5, 3, 3, false),
                ("EEE472", "Power System Design", "EEM", 5, 3, 3, false),

                // ── YM (Dept: YM) ──
                ("SENG322", "Software Validation and Verification", "YM", 6, 3, 0, false),
                ("SENG324", "Software Design Patterns", "YM", 5, 3, 0, false),
                ("CENG384", "Intro. to Signals Processing for Comp. Engineers", "YM", 5, 3, 0, false),
                ("SENG491", "Graduation Project I", "YM", 5, 3, 2, false),
                ("SENG492", "Graduation Project II", "YM", 5, 3, 2, false),
                ("SENG490", "Long Term Practice", "YM", 25, 0, 16, false),
            };

            var depts = context.Departments.ToDictionary(d => d.Code, d => d.Id);
            foreach (var c in courseData)
            {
                var deptCode = c.Dept ?? ResolveServiceUnitCode(c.Code);
                if (!depts.TryGetValue(deptCode, out var deptId))
                    throw new InvalidOperationException(
                        $"Seed: course '{c.Code}' expects department '{deptCode}' but it is not seeded. " +
                        "Add the service unit to SeedData.Academics or adjust ResolveServiceUnitCode.");
                context.Courses.Add(new Course
                {
                    Code = c.Code, Name = c.Name, DepartmentId = deptId,
                    Ects = c.Ects, TheoryHours = c.Th, LabHours = c.Lab, IsNonGpa = c.Ng
                });
            }
            await context.SaveChangesAsync();
        }

        // ============================================================
        // 2. CURRICULUM-COURSE LINKS (392 total)
        // ============================================================
        if (!context.Set<CurriculumCourse>().Any())
        {
            var curricula = context.Curricula.Include(c => c.Program).ToDictionary(c => c.Program.Code, c => c.Id);
            var courses = context.Courses.ToDictionary(c => c.Code, c => c.Id);

            // (progCode, courseCode, isCompulsory, suggestedSemester)
            var ccData = new (string P, string C, bool Comp, int Sem)[]
            {
                // ── BM-L ──
                ("BM-L","CENG101",true,1), ("BM-L","OHS101",true,1), ("BM-L","ENG101",true,1),
                ("BM-L","MATH101",true,1), ("BM-L","PHY101",true,1), ("BM-L","PHY103",true,1),
                ("BM-L","BIO101",true,1), ("BM-L","HIS101",true,1), ("BM-L","TUR101",true,1),
                ("BM-L","CENG102",true,2), ("BM-L","OHS102",true,2), ("BM-L","ENG102",true,2),
                ("BM-L","MATH102",true,2), ("BM-L","PHY102",true,2), ("BM-L","PHY104",true,2),
                ("BM-L","HIS102",true,2), ("BM-L","TUR102",true,2),
                ("BM-L","EEE203",true,3), ("BM-L","EEE281",true,3), ("BM-L","CENG213",true,3),
                ("BM-L","CENG223",true,3), ("BM-L","CENG241",true,3), ("BM-L","ENG201",true,3),
                ("BM-L","CENG214",true,4), ("BM-L","CENG256",true,4), ("BM-L","MATH224",true,4),
                ("BM-L","MATH206",true,4), ("BM-L","SENG264",true,4), ("BM-L","ENG202",true,4),
                ("BM-L","CENG301",true,5), ("BM-L","CENG351",true,5), ("BM-L","SENG321",true,5),
                ("BM-L","CENG361",true,5), ("BM-L","ENG301",true,5),
                ("BM-L","CENG332",true,6), ("BM-L","CENG336",true,6), ("BM-L","SENG324",true,6),
                ("BM-L","CENG384",true,6), ("BM-L","ENG302",true,6),
                ("BM-L","CENG491",true,7), ("BM-L","CCE401",true,7),
                ("BM-L","CENG492",true,8), ("BM-L","CENG490",true,8),
                // BM-L electives — managed via ElectiveGroup / CurriculumElectivePool

                // ── BMTR-L ──
                ("BMTR-L","BM101",true,1), ("BMTR-L","ISG101",true,1), ("BMTR-L","ING101",true,1),
                ("BMTR-L","MAT101",true,1), ("BMTR-L","FIZ101",true,1), ("BMTR-L","FIZ103",true,1),
                ("BMTR-L","HIS101",true,1), ("BMTR-L","TUR101",true,1),
                ("BMTR-L","BM102",true,2), ("BMTR-L","BIY101",true,2), ("BMTR-L","ISG102",true,2),
                ("BMTR-L","ING102",true,2), ("BMTR-L","MAT102",true,2), ("BMTR-L","FIZ102",true,2),
                ("BMTR-L","FIZ104",true,2), ("BMTR-L","HIS102",true,2), ("BMTR-L","TUR102",true,2),
                ("BMTR-L","BM203",true,3), ("BMTR-L","BM281",true,3), ("BMTR-L","BM213",true,3),
                ("BMTR-L","BM223",true,3), ("BMTR-L","BM241",true,3), ("BMTR-L","ING201",true,3),
                ("BMTR-L","BM214",true,4), ("BMTR-L","BM256",true,4), ("BMTR-L","MAT224",true,4),
                ("BMTR-L","MAT206",true,4), ("BMTR-L","BM264",true,4), ("BMTR-L","ING202",true,4),
                ("BMTR-L","BM301",true,5), ("BMTR-L","BM351",true,5), ("BMTR-L","BM321",true,5),
                ("BMTR-L","BM361",true,5), ("BMTR-L","ING301",true,5),
                ("BMTR-L","BM332",true,6), ("BMTR-L","BM336",true,6), ("BMTR-L","BM324",true,6),
                ("BMTR-L","BM384",true,6), ("BMTR-L","ING302",true,6),
                ("BMTR-L","BM491",true,7), ("BMTR-L","EYG401",true,7),
                ("BMTR-L","BM492",true,8), ("BMTR-L","BM490",true,8),
                // BMTR-L electives — managed via ElectiveGroup / CurriculumElectivePool

                // ── BSM-L ──
                ("BSM-L","CES101",true,1), ("BSM-L","OHS101",true,1), ("BSM-L","ENG101",true,1),
                ("BSM-L","MATH101",true,1), ("BSM-L","PHY101",true,1), ("BSM-L","PHY103",true,1),
                ("BSM-L","HIS101",true,1), ("BSM-L","TUR101",true,1),
                ("BSM-L","CENG102",true,2), ("BSM-L","OHS102",true,2), ("BSM-L","ENG102",true,2),
                ("BSM-L","MATH102",true,2), ("BSM-L","PHY102",true,2), ("BSM-L","PHY104",true,2),
                ("BSM-L","HIS102",true,2), ("BSM-L","TUR102",true,2), ("BSM-L","BIO101",true,2),
                ("BSM-L","ISE211",true,3), ("BSM-L","ISE203",true,3), ("BSM-L","CES213",true,3),
                ("BSM-L","CENG223",true,3), ("BSM-L","SENG231",true,3), ("BSM-L","ENG201",true,3),
                ("BSM-L","CENG214",true,4), ("BSM-L","ISE222",true,4), ("BSM-L","MATH204",true,4),
                ("BSM-L","MATH206",true,4), ("BSM-L","SENG264",true,4), ("BSM-L","ENG202",true,4),
                ("BSM-L","CENG351",true,5), ("BSM-L","CENG361",true,5), ("BSM-L","SENG321",true,5),
                ("BSM-L","CENG336",true,5), ("BSM-L","ENG301",true,5),
                ("BSM-L","ISE322",true,6), ("BSM-L","ISE332",true,6),
                ("BSM-L","ISE350",true,6), ("BSM-L","ENG302",true,6),
                ("BSM-L","ISE491",true,7), ("BSM-L","CCE401",true,7),
                ("BSM-L","ISE492",true,8), ("BSM-L","ISE490",true,8),
                // BSM-L electives — managed via ElectiveGroup / CurriculumElectivePool

                // ── EM-L ──
                ("EM-L","OHS101",true,1), ("EM-L","ENG101",true,1), ("EM-L","MATH101",true,1),
                ("EM-L","PHY101",true,1), ("EM-L","PHY103",true,1), ("EM-L","IE101",true,1),
                ("EM-L","CENG101",true,1), ("EM-L","HIS101",true,1), ("EM-L","TUR101",true,1),
                ("EM-L","OHS102",true,2), ("EM-L","ENG102",true,2), ("EM-L","MATH102",true,2),
                ("EM-L","PHY102",true,2), ("EM-L","PHY104",true,2), ("EM-L","IE112",true,2),
                ("EM-L","CENG102",true,2), ("EM-L","HIS102",true,2), ("EM-L","TUR102",true,2),
                ("EM-L","IE241",true,3), ("EM-L","IE227",true,3), ("EM-L","IE223",true,3),
                ("EM-L","MATH213",true,3), ("EM-L","MATH215",true,3), ("EM-L","ENG201",true,3),
                ("EM-L","IE202",true,4), ("EM-L","IE224",true,4), ("EM-L","IE228",true,4),
                ("EM-L","IE232",true,4), ("EM-L","IE242",true,4), ("EM-L","ENG202",true,4),
                ("EM-L","IE301",true,5), ("EM-L","IE333",true,5), ("EM-L","IE327",true,5),
                ("EM-L","IE365",true,5), ("EM-L","IE325",true,5), ("EM-L","ENG301",true,5),
                ("EM-L","IE324",true,6), ("EM-L","IE366",true,6), ("EM-L","IE346",true,6),
                ("EM-L","ENG302",true,6),
                ("EM-L","IE491",true,7), ("EM-L","IE489",true,7), ("EM-L","CCE401",true,7),
                ("EM-L","IE492",true,8), ("EM-L","IE490",true,8),
                // EM-L electives
                ("EM-L","IE352",false,0), ("EM-L","IE354",false,0), ("EM-L","IE356",false,0),
                ("EM-L","IE358",false,0), ("EM-L","IE362",false,0), ("EM-L","IE364",false,0),
                ("EM-L","IE372",false,0), ("EM-L","IE374",false,0), ("EM-L","IE376",false,0),
                ("EM-L","IE380",false,0), ("EM-L","IE382",false,0), ("EM-L","IE384",false,0),
                ("EM-L","IE386",false,0), ("EM-L","IE388",false,0), ("EM-L","EEE318",false,0),
                ("EM-L","EEE320",false,0), ("EM-L","ISE222",false,0), ("EM-L","SENG264",false,0),
                ("EM-L","ISE350",false,0), ("EM-L","IE451",false,0), ("EM-L","IE453",false,0),
                ("EM-L","IE455",false,0), ("EM-L","IE457",false,0), ("EM-L","IE461",false,0),
                ("EM-L","IE463",false,0), ("EM-L","IE465",false,0), ("EM-L","IE467",false,0),
                ("EM-L","IE469",false,0), ("EM-L","IE471",false,0), ("EM-L","IE473",false,0),
                ("EM-L","IE475",false,0), ("EM-L","IE477",false,0), ("EM-L","IE479",false,0),
                ("EM-L","IE481",false,0),
                ("EM-L","ISE211",false,0), ("EM-L","ISE361",false,0), ("EM-L","SENG231",false,0),
                ("EM-L","CENG213",false,0), ("EM-L","CENG375",false,0), ("EM-L","CENG351",false,0),
                // EM-L OC/NDE/TE — managed via ElectiveGroup / CurriculumElectivePool

                // ── EEM-L ──
                ("EEM-L","CENG101",true,1), ("EEM-L","ENG101",true,1), ("EEM-L","MATH101",true,1),
                ("EEM-L","PHY101",true,1), ("EEM-L","PHY103",true,1), ("EEM-L","BIO101",true,1),
                ("EEM-L","HIS101",true,1), ("EEM-L","TUR101",true,1), ("EEM-L","OHS101",true,1),
                ("EEM-L","EEE102",true,2), ("EEM-L","OHS102",true,2), ("EEM-L","ENG102",true,2),
                ("EEM-L","MATH102",true,2), ("EEM-L","PHY102",true,2), ("EEM-L","PHY104",true,2),
                ("EEM-L","HIS102",true,2), ("EEM-L","TUR102",true,2), ("EEM-L","CENG102",true,2),
                ("EEM-L","MATH201",true,3), ("EEM-L","EEE201",true,3), ("EEM-L","MATH204",true,3),
                ("EEM-L","EEE203",true,3), ("EEM-L","EEE205",true,3), ("EEM-L","ENG201",true,3),
                ("EEM-L","MATH206",true,4), ("EEM-L","EEE202",true,4), ("EEM-L","EEE208",true,4),
                ("EEM-L","EEE236",true,4), ("EEM-L","EEE204",true,4), ("EEM-L","EEE212",true,4),
                ("EEM-L","ENG202",true,4),
                ("EEM-L","EEE301",true,5), ("EEM-L","EEE303",true,5), ("EEM-L","EEE313",true,5),
                ("EEM-L","EEE331",true,5), ("EEM-L","EEE321",true,5), ("EEM-L","EEE315",true,5),
                ("EEM-L","AEE301",true,5),
                ("EEM-L","EEE302",true,6), ("EEM-L","EEE304",true,6), ("EEM-L","EEE306",true,6),
                ("EEM-L","ENG302",true,6),
                ("EEM-L","EEE491",true,7), ("EEM-L","CCE401",true,7),
                ("EEM-L","EEE492",true,8), ("EEM-L","EEE490",true,8),
                // EEM-L electives
                ("EEM-L","EEE312",false,0), ("EEM-L","EEE322",false,0), ("EEM-L","EEE324",false,0),
                ("EEM-L","EEE326",false,0), ("EEM-L","EEE328",false,0), ("EEM-L","EEE330",false,0),
                ("EEM-L","EEE332",false,0), ("EEM-L","EEE334",false,0), ("EEM-L","EEE338",false,0),
                ("EEM-L","EEE423",false,0), ("EEM-L","EEE455",false,0), ("EEM-L","EEE421",false,0),
                ("EEM-L","EEE425",false,0), ("EEM-L","EEE468",false,0), ("EEM-L","EEE471",false,0),
                ("EEM-L","EEE431",false,0), ("EEM-L","EEE433",false,0), ("EEM-L","EEE477",false,0),
                ("EEM-L","EEE435",false,0), ("EEM-L","EEE467",false,0), ("EEM-L","EEE484",false,0),
                ("EEM-L","EEE472",false,0),
                // EEM-L OC/NDE/TE — managed via ElectiveGroup / CurriculumElectivePool

                // ── YM-L ──
                ("YM-L","CENG101",true,1), ("YM-L","OHS101",true,1), ("YM-L","ENG101",true,1),
                ("YM-L","MATH101",true,1), ("YM-L","PHY101",true,1), ("YM-L","PHY103",true,1),
                ("YM-L","BIO101",true,1), ("YM-L","HIS101",true,1), ("YM-L","TUR101",true,1),
                ("YM-L","CENG102",true,2), ("YM-L","OHS102",true,2), ("YM-L","ENG102",true,2),
                ("YM-L","MATH102",true,2), ("YM-L","PHY102",true,2), ("YM-L","PHY104",true,2),
                ("YM-L","HIS102",true,2), ("YM-L","TUR102",true,2),
                ("YM-L","SENG231",true,3), ("YM-L","EEE203",true,3), ("YM-L","CENG213",true,3),
                ("YM-L","CENG223",true,3), ("YM-L","CENG241",true,3), ("YM-L","ENG201",true,3),
                ("YM-L","CENG214",true,4), ("YM-L","CENG256",true,4), ("YM-L","MATH224",true,4),
                ("YM-L","MATH206",true,4), ("YM-L","SENG264",true,4), ("YM-L","ENG202",true,4),
                ("YM-L","CENG301",true,5), ("YM-L","CENG351",true,5), ("YM-L","SENG321",true,5),
                ("YM-L","CENG361",true,5), ("YM-L","ENG301",true,5),
                ("YM-L","SENG322",true,6), ("YM-L","SENG324",true,6), ("YM-L","CENG336",true,6),
                ("YM-L","CENG384",true,6), ("YM-L","ENG302",true,6),
                ("YM-L","SENG491",true,7), ("YM-L","CCE401",true,7),
                ("YM-L","SENG492",true,8), ("YM-L","SENG490",true,8),
                // YM-L electives — managed via ElectiveGroup / CurriculumElectivePool
            };

            var existingCurriculumCourses = new HashSet<(int CurriculumId, int CourseId)>(
    context.Set<CurriculumCourse>()
        .Select(x => new ValueTuple<int, int>(x.CurriculumId, x.CourseId))
        .ToList()
);

foreach (var (p, c, comp, sem) in ccData)
{
    if (!courses.ContainsKey(c)) continue;
    if (!curricula.ContainsKey(p)) continue;

    var curriculumId = curricula[p];
    var courseId = courses[c];
    var key = (curriculumId, courseId);

    if (existingCurriculumCourses.Contains(key))
        continue;

    context.Set<CurriculumCourse>().Add(new CurriculumCourse
    {
        CurriculumId = curriculumId,
        CourseId = courseId,
        IsCompulsory = comp,
        SuggestedSemester = sem
    });

    existingCurriculumCourses.Add(key);
}
            await context.SaveChangesAsync();
        }

        // ============================================================
        // 3. PREREQUISITES (16 total)
        // ============================================================
        if (!context.Set<CoursePrerequisite>().Any())
        {
            var courses = context.Courses.ToDictionary(c => c.Code, c => c.Id);
            var prereqs = new (string Course, string Required)[]
            {
                ("CENG102", "CENG101"), ("MATH102", "MATH101"),
                ("CENG213", "CENG101"), ("MATH224", "MATH101"),
                ("MATH206", "MATH101"), ("SENG264", "CENG101"),
                ("SENG264", "MATH101"),
                ("CENG491", "CENG101"), ("CENG491", "MATH101"),
                ("CENG491", "PHY101"), ("CENG491", "PHY103"),
                ("CENG491", "CENG102"), ("CENG491", "MATH102"),
                ("CENG491", "PHY102"), ("CENG491", "PHY104"),
                ("CENG492", "CENG491"),
            };

            foreach (var (course, required) in prereqs)
            {
                if (courses.ContainsKey(course) && courses.ContainsKey(required))
                {
                    context.Set<CoursePrerequisite>().Add(new CoursePrerequisite
                    {
                        CourseId = courses[course],
                        RequiredCourseId = courses[required]
                    });
                }
            }
            await context.SaveChangesAsync();
        }
    }

    // Ortak dersler (Dept = null) için course prefix'ine göre ilgili service unit
    // koordinatörlüğünü çözümler. SeedData.Academics.cs'te TBK/AIIT/TRD/YDL
    // zaten tanımlı. Yeni bir ortak ders prefix'i eklendiğinde burası da güncellenmeli.
    private static string ResolveServiceUnitCode(string courseCode)
    {
        var prefix = new string(courseCode.TakeWhile(char.IsLetter).ToArray());
        return prefix switch
        {
            "MAT" or "MATH" or "FIZ" or "PHY" or "BIY" or "BIO"
                or "ISG" or "OHS" or "EYG" or "CCE" => "TBK",
            "ING" or "ENG" or "AEE" => "YDL",
            "HIS" or "AIIT" => "AIIT",
            "TUR" or "TRD" => "TRD",
            _ => throw new InvalidOperationException(
                $"Seed: no service unit mapping for course prefix '{prefix}' (course '{courseCode}'). " +
                "Set an explicit Dept in courseData or add a mapping in ResolveServiceUnitCode."),
        };
    }
}
