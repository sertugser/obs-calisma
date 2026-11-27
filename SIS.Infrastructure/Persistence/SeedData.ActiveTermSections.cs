using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    // Örnek veri, Adım 3.7b: etkin dönemde müfredatın o döneme düşen zorunlu dersleri için birer şube
    // açılır, her şubeye bölümünden bir öğretim elemanı atanır ve haftalık ders saati kadar program
    // satırı yazılır. Yerleştirme basittir (program motoru çağrılmaz): aynı hoca, aynı derslik ve aynı
    // müfredat yarıyılı aynı zaman diliminde iki kez kullanılmaz. Şube zaten varsa hiçbir şey yazılmaz.
    internal static async Task SeedActiveTermSectionsAsync(AppDbContext context, DateTime bugun)
    {
        var donem = await context.AcademicTerms.ActiveTerm().FirstOrDefaultAsync();
        if (donem is null || await context.CourseSections.AnyAsync(s => s.TermId == donem.Id)) return;

        var dilimler = await context.TimeSlots.Where(t => t.TermId == donem.Id)
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.SlotOrder).ToListAsync();
        var derslikler = await context.Classrooms.OrderBy(c => c.Id).ToListAsync();
        var hocalar = (await context.Instructors.OrderBy(i => i.Id).ToListAsync())
            .GroupBy(i => i.DepartmentId).ToDictionary(g => g.Key, g => g.ToList());
        if (dilimler.Count == 0 || hocalar.Count == 0) return;

        var tek = donem.TermType == TermType.Fall;
        var dersler = (await context.Set<CurriculumCourse>().Include(cc => cc.Course)
                .Where(cc => cc.IsCompulsory && cc.SuggestedSemester > 0).ToListAsync())
            .Where(cc => cc.SuggestedSemester % 2 == (tek ? 1 : 0))
            .GroupBy(cc => cc.CourseId)
            .Select(g => (Ders: g.First().Course, Yariyil: g.Min(cc => cc.SuggestedSemester), Mufredat: g.First().CurriculumId))
            .OrderBy(d => d.Ders.Code, StringComparer.Ordinal)
            .ToList();

        var hocaDolu = new HashSet<(int, int)>();
        var derslikDolu = new HashSet<(int, int)>();
        var yariyilDolu = new HashSet<(int, int, int)>();
        var sira = new Dictionary<int, int>();

        foreach (var (ders, yariyil, mufredat) in dersler)
        {
            var bolum = ders.DepartmentId ?? hocalar.Keys.First();
            var aday = hocalar.GetValueOrDefault(bolum) ?? hocalar.Values.First();
            var hoca = aday[sira.GetValueOrDefault(bolum) % aday.Count];
            sira[bolum] = sira.GetValueOrDefault(bolum) + 1;

            var sube = new CourseSection
            {
                CourseId = ders.Id,
                TermId = donem.Id,
                InstructorId = hoca.Id,
                Capacity = 60,
                SectionNumber = 1,
                Status = CourseSectionStatus.Active,
                CreatedAt = bugun,
            };
            context.CourseSections.Add(sube);

            var saat = Math.Max(1, ders.TheoryHours + ders.LabHours);
            foreach (var dilim in dilimler)
            {
                if (saat == 0) break;
                if (hocaDolu.Contains((hoca.Id, dilim.Id)) || yariyilDolu.Contains((mufredat, yariyil, dilim.Id))) continue;
                var derslik = derslikler.FirstOrDefault(c => !derslikDolu.Contains((c.Id, dilim.Id)));
                if (derslik is null) continue;

                hocaDolu.Add((hoca.Id, dilim.Id));
                derslikDolu.Add((derslik.Id, dilim.Id));
                yariyilDolu.Add((mufredat, yariyil, dilim.Id));
                sube.SectionSchedules.Add(new SectionSchedule { TimeSlotId = dilim.Id, ClassroomId = derslik.Id });
                saat--;
            }
        }

        await context.SaveChangesAsync();
    }
}
