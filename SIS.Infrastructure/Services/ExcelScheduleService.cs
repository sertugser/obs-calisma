using ClosedXML.Excel;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class ExcelScheduleService : IExcelScheduleService
{
    private static readonly TimeSpan ImportSessionLifetime = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions SessionJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public ExcelScheduleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportScheduleAsync(
        int termId,
        int facultyId,
        int? departmentId = null,
        CancellationToken ct = default)
    {
        // Load all section-schedules for the term/faculty, optionally filtered by department
        var query = _db.SectionSchedules
            .Where(ss => ss.CourseSection.TermId == termId
                      && ss.CourseSection.Course.Department!.FacultyId == facultyId)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(ss => ss.CourseSection.Course.DepartmentId == departmentId.Value);
        }

        var rows = await query
            .Select(ss => new
            {
                CourseCode = ss.CourseSection.Course.Code,
                CourseName = ss.CourseSection.Course.Name,
                SectionNumber = ss.CourseSection.SectionNumber,
                InstructorUserId = ss.CourseSection.Instructor != null ? ss.CourseSection.Instructor.UserId : (int?)null,
                InstructorTitle = ss.CourseSection.Instructor != null ? ss.CourseSection.Instructor.Title : string.Empty,
                DayOfWeek = ss.TimeSlot.DayOfWeek,
                StartTime = ss.TimeSlot.StartTime,
                EndTime = ss.TimeSlot.EndTime,
                SlotOrder = ss.TimeSlot.SlotOrder,
                ClassroomName = ss.Classroom!.Name,
                Capacity = ss.CourseSection.Capacity,
                IsActive = ss.CourseSection.IsActive,
                IsFinalized = ss.CourseSection.IsFinalized,
            })
            .OrderBy(r => r.CourseCode)
            .ThenBy(r => r.SectionNumber)
            .ThenBy(r => r.DayOfWeek)
            .ThenBy(r => r.SlotOrder)
            .ToListAsync(ct);

        // Collect instructor user IDs to fetch names
        var userIds = rows
            .Where(r => r.InstructorUserId.HasValue)
            .Select(r => r.InstructorUserId!.Value)
            .Distinct()
            .ToList();

        var userNames = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        // Derslik-bazlı pivot için fakülteye atanmış tüm derslikleri çek
        // (atanmış olanlar = bu dönem bu fakültenin kullanım kapsamında).
        var pivotClassrooms = await _db.ClassroomTermAssignments
            .Where(cta => cta.TermId == termId && cta.FacultyId == facultyId)
            .Select(cta => new { cta.Classroom!.Id, cta.Classroom.Name, cta.Classroom.Capacity })
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        // Tüm TimeSlot grid'i (dönem bazında, sabit)
        var allSlots = await _db.TimeSlots
            .Where(ts => ts.TermId == termId)
            .OrderBy(ts => ts.DayOfWeek).ThenBy(ts => ts.SlotOrder)
            .Select(ts => new { ts.DayOfWeek, ts.StartTime, ts.EndTime, ts.SlotOrder })
            .ToListAsync(ct);

        // Fakülte kısa kodu (header altında gösterilir — MMF gibi)
        var facultyInfo = await _db.Faculties
            .Where(f => f.Id == facultyId)
            .Select(f => new { f.Code, f.Name })
            .FirstOrDefaultAsync(ct);
        var facultyCode = facultyInfo == null
            ? string.Empty
            : (string.IsNullOrWhiteSpace(facultyInfo.Code) ? facultyInfo.Name : facultyInfo.Code);

        using var workbook = new XLWorkbook();

        // ── Sheet 1: Derslik Bazlı Program (YENİ — ana görünüm) ─────────────
        // Klasik üniversite ders programı formatı:
        // Satır: Gün (merged) + Slot# + Saat | Sütun: her bir derslik
        BuildClassroomPivotSheet(workbook, rows, userNames, pivotClassrooms, allSlots, facultyCode);

        // ── Sheet 2: Program Verisi ──────────────────────────────────────────
        var dataSheet = workbook.Worksheets.Add("Program Verisi");

        var headers = new[]
        {
            "Ders Kodu", "Ders Adı", "Şube", "Öğretim Üyesi",
            "Gün", "Başlangıç", "Bitiş", "Derslik", "Kapasite", "Durum"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = dataSheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1F, 0x49, 0x7D);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int dataRow = 2;
        foreach (var row in rows)
        {
            string instructorName = row.InstructorUserId.HasValue
                ? userNames.TryGetValue(row.InstructorUserId.Value, out var name) ? name : "-"
                : "-";

            if (!string.IsNullOrWhiteSpace(row.InstructorTitle) && instructorName != "-")
                instructorName = $"{row.InstructorTitle} {instructorName}";

            string status = row.IsFinalized ? "Kesinleşti"
                          : row.IsActive    ? "Aktif"
                                            : "Pasif";

            dataSheet.Cell(dataRow, 1).Value = row.CourseCode;
            dataSheet.Cell(dataRow, 2).Value = row.CourseName;
            dataSheet.Cell(dataRow, 3).Value = row.SectionNumber;
            dataSheet.Cell(dataRow, 4).Value = instructorName;
            dataSheet.Cell(dataRow, 5).Value = DayOfWeekTurkish(row.DayOfWeek);
            dataSheet.Cell(dataRow, 6).Value = row.StartTime.ToString(@"HH\:mm");
            dataSheet.Cell(dataRow, 7).Value = row.EndTime.ToString(@"HH\:mm");
            dataSheet.Cell(dataRow, 8).Value = row.ClassroomName;
            dataSheet.Cell(dataRow, 9).Value = row.Capacity;
            dataSheet.Cell(dataRow, 10).Value = status;

            // Zebra striping
            if (dataRow % 2 == 0)
            {
                dataSheet.Row(dataRow).Style.Fill.BackgroundColor = XLColor.FromArgb(0xEF, 0xF4, 0xFF);
            }

            dataRow++;
        }

        dataSheet.Columns().AdjustToContents();

        // Min sütun genişlikleri — AdjustToContents bazen çok daraltıyor.
        // Bu garantili minimum okunabilirlik için.
        if (dataSheet.Column(1).Width < 12) dataSheet.Column(1).Width = 12;  // Ders Kodu
        if (dataSheet.Column(2).Width < 35) dataSheet.Column(2).Width = 35;  // Ders Adı
        if (dataSheet.Column(3).Width < 8)  dataSheet.Column(3).Width = 8;   // Şube
        if (dataSheet.Column(4).Width < 28) dataSheet.Column(4).Width = 28;  // Öğretim Üyesi
        if (dataSheet.Column(5).Width < 12) dataSheet.Column(5).Width = 12;  // Gün
        if (dataSheet.Column(6).Width < 10) dataSheet.Column(6).Width = 10;  // Başlangıç
        if (dataSheet.Column(7).Width < 10) dataSheet.Column(7).Width = 10;  // Bitiş
        if (dataSheet.Column(8).Width < 18) dataSheet.Column(8).Width = 18;  // Derslik
        if (dataSheet.Column(9).Width < 10) dataSheet.Column(9).Width = 10;  // Kapasite
        if (dataSheet.Column(10).Width < 12) dataSheet.Column(10).Width = 12; // Durum

        dataSheet.Row(1).Height = 24;
        dataSheet.SheetView.FreezeRows(1);

        // Add auto-filter on header row
        if (dataRow > 2)
        {
            dataSheet.RangeUsed()?.SetAutoFilter();
        }

        // ── Sheet 3: Haftalık Grid (renkli, multi-row kart layout) ─────────
        // Her ders AYRI bir Excel satırında, ders kodu prefix'ine göre kendi renginde.
        // Saat sütunu, o saat dilimindeki en kalabalık günün ders sayısı kadar satıra
        // MERGE edilir → tek lacivert "saat hücresi" görselliği. Kalın dış border + ince
        // iç border → her ders kartı net ayırt edilebilir.
        var gridSheet = workbook.Worksheets.Add("Haftalık Grid");

        // Hafta içi sabit + veride GERÇEKTEN olan hafta sonu günleri (Cmt/Paz). Hafta sonu slotu
        // programlanabildiği için (bkz. weekend search) o günlerde ders varsa grid'de de görünmeli;
        // yoksa grid hafta içiyle dar kalır (her zaman 7 sütuna genişletme yok).
        var presentDays = rows.Select(r => r.DayOfWeek).ToHashSet();
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Concat(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }.Where(presentDays.Contains))
            .ToArray();

        // Collect distinct slot orders for the term
        var slotOrders = rows
            .Select(r => (r.SlotOrder, r.StartTime, r.EndTime))
            .Distinct()
            .OrderBy(s => s.SlotOrder)
            .ToList();

        // ── Header row: col 1 = "Saat / Gün", cols 2-6 = days ─────────────
        var headerSaatCell = gridSheet.Cell(1, 1);
        headerSaatCell.Value = "Saat / Gün";
        headerSaatCell.Style.Font.Bold = true;
        headerSaatCell.Style.Font.FontSize = 11;
        headerSaatCell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1F, 0x49, 0x7D);
        headerSaatCell.Style.Font.FontColor = XLColor.White;
        headerSaatCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerSaatCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        for (int d = 0; d < days.Length; d++)
        {
            var cell = gridSheet.Cell(1, d + 2);
            cell.Value = DayOfWeekTurkish(days[d]);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 11;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1F, 0x49, 0x7D);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // Build a lookup: (slotOrder, dayOfWeek) → list of section "cards"
        // (kod-şube, hoca, derslik üçlüsü — her ders kendi satırına yerleşecek)
        var gridLookup = new Dictionary<(int slotOrder, DayOfWeek day), List<(string code, string instructor, string classroom)>>();

        foreach (var row in rows)
        {
            string instructorName = row.InstructorUserId.HasValue
                ? userNames.TryGetValue(row.InstructorUserId.Value, out var name) ? name : "-"
                : "-";

            var card = ($"{row.CourseCode}-{row.SectionNumber}", instructorName, row.ClassroomName);
            var key = (row.SlotOrder, row.DayOfWeek);

            if (!gridLookup.ContainsKey(key))
                gridLookup[key] = new List<(string, string, string)>();

            gridLookup[key].Add(card);
        }

        // ── Fill slot blocks (multi-row layout) ────────────────────────────
        // Her ders kartı 1 Excel satırı (yükseklik ~45pt). O saat dilimindeki en kalabalık
        // günün ders sayısı kadar Excel satırı ayrılır; saat sütunu bu satırların tümünde
        // MERGE edilir → tek lacivert "saat aralığı" hücresi.
        const double CardRowHeight = 45;
        int currentRow = 2;

        for (int s = 0; s < slotOrders.Count; s++)
        {
            var (slotOrder, startTime, endTime) = slotOrders[s];

            // Bu slot'ta her gün için ders kartı listesi (boşsa empty list)
            var cardsPerDay = days
                .Select(d => gridLookup.TryGetValue((slotOrder, d), out var list)
                    ? list
                    : new List<(string, string, string)>())
                .ToList();
            int maxCards = Math.Max(1, cardsPerDay.Max(list => list.Count));

            int blockStart = currentRow;
            int blockEnd = currentRow + maxCards - 1;

            // Saat sütunu — tüm blok satırlarını kapsayan tek hücre (merge)
            var labelRange = gridSheet.Range(blockStart, 1, blockEnd, 1);
            labelRange.Merge();
            labelRange.Value = $"{startTime:HH\\:mm}-{endTime:HH\\:mm}";
            labelRange.Style.Font.Bold = true;
            labelRange.Style.Font.FontSize = 12;
            labelRange.Style.Font.FontColor = XLColor.White;
            labelRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1F, 0x49, 0x7D);
            labelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            labelRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Her gün için, her ders kartını ayrı satıra yerleştir
            for (int d = 0; d < days.Length; d++)
            {
                var cardsHere = cardsPerDay[d];
                for (int i = 0; i < maxCards; i++)
                {
                    var dataCell = gridSheet.Cell(blockStart + i, d + 2);

                    if (i < cardsHere.Count)
                    {
                        var (code, instructor, classroom) = cardsHere[i];
                        dataCell.Value = $"{code}\n{instructor}\n{classroom}";
                        dataCell.Style.Alignment.WrapText = true;
                        var (r, g, b) = GetCourseCardColor(code);
                        dataCell.Style.Fill.BackgroundColor = XLColor.FromArgb(r, g, b);
                        dataCell.Style.Font.FontSize = 10;
                        // Ders kodu satırı için font default kalır; istenirse RichText ile
                        // kod kalın yapılabilir — şu an WrapText + line break yeterli okunaklılık.
                    }
                    else
                    {
                        // O saatte o günde ders yok — açık gri, görsel olarak dolu hücrelerden ayırt edilebilir
                        dataCell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xF8, 0xF9, 0xFA);
                    }

                    dataCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    dataCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }

            // Her kart satırına sabit yükseklik (3 satır metin için yeterli)
            for (int i = 0; i < maxCards; i++)
                gridSheet.Row(blockStart + i).Height = CardRowHeight;

            currentRow = blockEnd + 1;
        }

        // ── Sütun genişlikleri (sabit + eşit) ──
        gridSheet.Column(1).Width = 15;                       // Saat sütunu
        for (int d = 2; d <= 1 + days.Length; d++)
            gridSheet.Column(d).Width = 28;                   // Her gün sütunu eşit

        // ── Header satırı yüksekliği ──
        gridSheet.Row(1).Height = 30;

        // ── Tüm hücrelere border (görsel ayrım) ──
        // Dış border kalın (kart bloklarını çerçeveler), iç border ince (ders ayrımı için)
        int lastDataRow = currentRow - 1;
        var gridRange = gridSheet.Range(1, 1, lastDataRow, days.Length + 1);
        gridRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        gridRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;

        gridSheet.SheetView.FreezeRows(1);
        gridSheet.SheetView.FreezeColumns(1);

        // ── Serialize to byte array ──────────────────────────────────────────
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportPdfAsync(
        int termId,
        int facultyId,
        int? departmentId = null,
        CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // ── Load metadata ────────────────────────────────────────────────────
        var term = await _db.AcademicTerms
            .Where(t => t.Id == termId)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Dönem bulunamadı: {termId}");

        var faculty = await _db.Faculties
            .Where(f => f.Id == facultyId)
            .Select(f => new { f.Id, f.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Fakülte bulunamadı: {facultyId}");

        // Departments to export
        var departmentsQuery = _db.Departments.Where(d => d.FacultyId == facultyId);
        if (departmentId.HasValue)
            departmentsQuery = departmentsQuery.Where(d => d.Id == departmentId.Value);

        var departments = await departmentsQuery
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(ct);

        // ── Load section-schedule data ───────────────────────────────────────
        var baseQuery = _db.SectionSchedules
            .Where(ss => ss.CourseSection.TermId == termId
                      && ss.CourseSection.Course.Department!.FacultyId == facultyId);

        if (departmentId.HasValue)
            baseQuery = baseQuery.Where(ss => ss.CourseSection.Course.DepartmentId == departmentId.Value);

        var rows = await baseQuery
            .Select(ss => new
            {
                DepartmentId   = ss.CourseSection.Course.DepartmentId,
                CourseCode     = ss.CourseSection.Course.Code,
                CourseName     = ss.CourseSection.Course.Name,
                SectionNumber  = ss.CourseSection.SectionNumber,
                Capacity       = ss.CourseSection.Capacity,
                InstructorUserId = ss.CourseSection.Instructor != null ? ss.CourseSection.Instructor.UserId : (int?)null,
                InstructorTitle  = ss.CourseSection.Instructor != null ? ss.CourseSection.Instructor.Title : string.Empty,
                DayOfWeek      = ss.TimeSlot.DayOfWeek,
                StartTime      = ss.TimeSlot.StartTime,
                EndTime        = ss.TimeSlot.EndTime,
                SlotOrder      = ss.TimeSlot.SlotOrder,
                ClassroomName  = ss.Classroom!.Name,
            })
            .OrderBy(r => r.CourseCode)
            .ThenBy(r => r.SectionNumber)
            .ThenBy(r => r.DayOfWeek)
            .ThenBy(r => r.SlotOrder)
            .ToListAsync(ct);

        // Fetch instructor names
        var userIds = rows.Where(r => r.InstructorUserId.HasValue)
                          .Select(r => r.InstructorUserId!.Value)
                          .Distinct().ToList();

        var userNames = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        // ── PDF sadeleştirmesi: sadece dersi olan bölümleri dahil et ────────
        // (Aksi halde 10 bölümden 9'u boş "Bu bölüm için programlanmış ders
        // bulunmamaktadır." sayfaları üretiyor → PDF aşırı şişiyor)
        var departmentsWithData = rows
            .Where(r => r.DepartmentId.HasValue)
            .Select(r => r.DepartmentId!.Value)
            .ToHashSet();
        departments = departments
            .Where(d => departmentsWithData.Contains(d.Id))
            .ToList();

        // ── Derslik-bazlı pivot için fakülteye atanmış tüm derslikleri çek ──
        var pivotClassrooms = await _db.ClassroomTermAssignments
            .Where(cta => cta.TermId == termId && cta.FacultyId == facultyId)
            .Select(cta => new { cta.Classroom!.Id, cta.Classroom.Name, cta.Classroom.Capacity })
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var allSlots = await _db.TimeSlots
            .Where(ts => ts.TermId == termId)
            .OrderBy(ts => ts.DayOfWeek).ThenBy(ts => ts.SlotOrder)
            .Select(ts => new { ts.DayOfWeek, ts.StartTime, ts.EndTime, ts.SlotOrder })
            .ToListAsync(ct);

        // ── Helpers ──────────────────────────────────────────────────────────
        string InstructorDisplay(int? uid, string title)
        {
            // Hoca atanmamışsa boş döner — PDF/Excel'de "-" placeholder gözükmesin
            if (uid == null) return string.Empty;
            if (!userNames.TryGetValue(uid.Value, out var name)) return string.Empty;
            return string.IsNullOrWhiteSpace(title) ? name : $"{title} {name}";
        }

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        // Derslik-bazlı pivot için lookup: (day, slotOrder, classroomName) → cellText
        var pivotLookup = new Dictionary<(DayOfWeek, int, string), string>();
        foreach (var r in rows)
        {
            var instructor = InstructorDisplay(r.InstructorUserId, r.InstructorTitle);
            // Hoca atanmamışsa son satıra boş eklenmez — daha temiz görünüm
            string cellText = string.IsNullOrEmpty(instructor)
                ? $"{r.CourseCode}-{r.SectionNumber}\n{r.CourseName}"
                : $"{r.CourseCode}-{r.SectionNumber}\n{r.CourseName}\n{instructor}";
            pivotLookup[(r.DayOfWeek, r.SlotOrder, r.ClassroomName)] = cellText;
        }

        // ── Build PDF document ───────────────────────────────────────────────
        var document = Document.Create(container =>
        {
            bool isAllDepts = !departmentId.HasValue;

            // Cover page (all-departments mode only)
            if (isAllDepts)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(2, Unit.Centimetre);
                    page.Content().Column(col =>
                    {
                        col.Spacing(20);
                        col.Item().AlignCenter().Text("ANKARA BİLİM ÜNİVERSİTESİ")
                            .FontSize(22).Bold();
                        col.Item().AlignCenter().Text(faculty.Name)
                            .FontSize(16).SemiBold();
                        col.Item().AlignCenter().Text($"Ders Programları — {term.Name}")
                            .FontSize(14);
                        col.Item().PaddingTop(20).Text("İçindekiler").FontSize(12).Bold();
                        foreach (var dept in departments)
                        {
                            col.Item().Text($"• {dept.Name}").FontSize(11);
                        }
                    });
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span($"Oluşturulma: {TurkeyNow():dd.MM.yyyy HH:mm}").FontSize(8);
                    });
                });
            }

            // ── Fakülte genelinde derslik-bazlı pivot sayfası ────────────────
            // (Klasik üniversite ders programı görünümü — Proliz tarzı)
            if (pivotClassrooms.Count > 0)
            {
                // Pivot lookup'ta aynı (day, slotOrder) için kaç farklı derslik var?
                // Birden fazlaysa o satırdaki dolu hücreler sarı vurgu ile gösterilir.
                var slotMultiKeys = new HashSet<(DayOfWeek, int)>();
                var slotCount = new Dictionary<(DayOfWeek, int), int>();
                foreach (var k in pivotLookup.Keys)
                {
                    var sk = (k.Item1, k.Item2);
                    slotCount[sk] = slotCount.GetValueOrDefault(sk) + 1;
                }
                foreach (var (k, v) in slotCount)
                    if (v > 1) slotMultiKeys.Add(k);

                // Derslik başına dolu slot sayısı (kullanım oranı için)
                var classroomFilled = new int[pivotClassrooms.Count];
                int totalSlotRows = 0;

                container.Page(page =>
                {
                    page.Size(PageSizes.A3.Landscape());
                    page.Margin(1f, Unit.Centimetre);

                    page.Header().Column(hdr =>
                    {
                        hdr.Item().Text("ANKARA BİLİM ÜNİVERSİTESİ").FontSize(13).Bold().AlignCenter();
                        hdr.Item().Text(faculty.Name).FontSize(11).AlignCenter();
                        hdr.Item().Text($"Derslik Bazlı Haftalık Program — {term.Name}").FontSize(10).AlignCenter();
                        hdr.Item().PaddingTop(4).LineHorizontal(1);
                    });

                    // Renk paleti (Excel ile aynı)
                    var headerColor    = "#1F497D";
                    var dayColor       = "#4F81BD";
                    var slotColor      = "#DBE5F1";
                    var highlightColor = "#FFFF99";
                    var summaryColor   = "#F2F2F2";

                    page.Content().PaddingTop(6).Table(table =>
                    {
                        // Sütunlar: Gün + Slot# + Saat + her derslik
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(50);    // Gün
                            cols.ConstantColumn(22);    // Slot #
                            cols.ConstantColumn(55);    // Saat
                            foreach (var _ in pivotClassrooms)
                                cols.RelativeColumn(3); // Her derslik eşit pay
                        });

                        // Üst header satırı: kolon başlıkları
                        table.Header(header =>
                        {
                            header.Cell().Background(headerColor).Padding(3)
                                .AlignCenter().AlignMiddle()
                                .Text("Gün").FontColor(Colors.White).FontSize(8).Bold();
                            header.Cell().Background(headerColor).Padding(3)
                                .AlignCenter().AlignMiddle()
                                .Text("#").FontColor(Colors.White).FontSize(8).Bold();
                            header.Cell().Background(headerColor).Padding(3)
                                .AlignCenter().AlignMiddle()
                                .Text("Saat").FontColor(Colors.White).FontSize(8).Bold();
                            foreach (var cr in pivotClassrooms)
                            {
                                header.Cell().Background(headerColor).Padding(3)
                                    .AlignCenter().AlignMiddle()
                                    .Text(t =>
                                    {
                                        t.Span(cr.Name).FontColor(Colors.White).FontSize(7).Bold();
                                        t.Line(string.Empty);
                                        t.Span($"({cr.Capacity} Kişi)").FontColor(Colors.White).FontSize(6);
                                    });
                            }
                        });

                        // Slot'ları güne göre grupla
                        var pivotSlotsByDay = allSlots
                            .GroupBy(s => s.DayOfWeek)
                            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SlotOrder).ToList());

                        var pivotDays = new[]
                        {
                            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
                        };

                        foreach (var day in pivotDays)
                        {
                            if (!pivotSlotsByDay.TryGetValue(day, out var daySlots) || daySlots.Count == 0)
                                continue;

                            for (int sIdx = 0; sIdx < daySlots.Count; sIdx++)
                            {
                                var slot = daySlots[sIdx];
                                bool isMulti = slotMultiKeys.Contains((day, slot.SlotOrder));
                                totalSlotRows++;

                                // Gün hücresi: sadece ilk satırda yazılsın
                                // (QuestPDF'te row-span yok — sıradaki satırlarda boş bırak)
                                table.Cell().Background(sIdx == 0 ? dayColor : dayColor)
                                    .BorderBottom(0.5f).BorderColor(Colors.Grey.Medium)
                                    .Padding(2).AlignCenter().AlignMiddle()
                                    .Text(sIdx == 0 ? DayOfWeekTurkish(day) : string.Empty)
                                    .FontSize(8).Bold().FontColor(Colors.White);

                                // Slot # hücresi
                                table.Cell().Background(slotColor)
                                    .Border(0.3f).BorderColor(Colors.Grey.Lighten1)
                                    .Padding(2).AlignCenter().AlignMiddle()
                                    .Text((sIdx + 1).ToString()).FontSize(8).Bold();

                                // Saat hücresi
                                table.Cell().Background(slotColor)
                                    .Border(0.3f).BorderColor(Colors.Grey.Lighten1)
                                    .Padding(2).AlignCenter().AlignMiddle()
                                    .Text($"{slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}")
                                    .FontSize(7).Bold();

                                // Her derslik için hücre
                                for (int cIdx = 0; cIdx < pivotClassrooms.Count; cIdx++)
                                {
                                    var cr = pivotClassrooms[cIdx];
                                    var key = (day, slot.SlotOrder, cr.Name);
                                    var hasContent = pivotLookup.ContainsKey(key);
                                    var cellBg = hasContent && isMulti ? highlightColor : "#FFFFFF";
                                    var cellText = hasContent ? pivotLookup[key] : string.Empty;
                                    if (hasContent) classroomFilled[cIdx]++;

                                    table.Cell().Background(cellBg)
                                        .Border(0.3f).BorderColor(Colors.Grey.Lighten1)
                                        .Padding(2).AlignCenter().AlignMiddle()
                                        .Text(cellText).FontSize(6);
                                }
                            }
                        }

                        // Derslik Kullanım Oranı satırı
                        // Span 3 (Gün + Slot# + Saat) ColumnSpan ile birleştirilemez QuestPDF'te.
                        // Bu yüzden 3 ayrı hücre olarak boş bırakıp son sütunlara değer veriyoruz.
                        table.Cell().Background(summaryColor).Padding(3)
                            .AlignRight().AlignMiddle()
                            .Text("Derslik").FontSize(7).Bold();
                        table.Cell().Background(summaryColor).Padding(3)
                            .AlignCenter().AlignMiddle()
                            .Text("Kullanım").FontSize(7).Bold();
                        table.Cell().Background(summaryColor).Padding(3)
                            .AlignCenter().AlignMiddle()
                            .Text("Oranı").FontSize(7).Bold();
                        for (int cIdx = 0; cIdx < pivotClassrooms.Count; cIdx++)
                        {
                            var pct = totalSlotRows > 0
                                ? (double)classroomFilled[cIdx] / totalSlotRows * 100.0
                                : 0.0;
                            table.Cell().Background(summaryColor).Border(0.3f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .Padding(3).AlignCenter().AlignMiddle()
                                .Text($"%{pct:0.00}").FontSize(8).Bold();
                        }
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"Oluşturulma: {TurkeyNow():dd.MM.yyyy HH:mm} · {pivotClassrooms.Count} derslik")
                            .FontSize(7);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Sayfa ").FontSize(7);
                            x.CurrentPageNumber().FontSize(7);
                            x.Span(" / ").FontSize(7);
                            x.TotalPages().FontSize(7);
                        });
                    });
                });
            }

            // Per-department pages
            foreach (var dept in departments)
            {
                var deptRows = rows.Where(r => r.DepartmentId == dept.Id).ToList();

                // ── Distinct time slots for this department ────────────────
                var slotOrders = deptRows
                    .Select(r => (r.SlotOrder, r.StartTime, r.EndTime))
                    .Distinct()
                    .OrderBy(s => s.SlotOrder)
                    .ToList();

                // ── Page 1: Weekly Grid ────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);

                    page.Header().Column(hdr =>
                    {
                        hdr.Item().Text("ANKARA BİLİM ÜNİVERSİTESİ").FontSize(13).Bold().AlignCenter();
                        hdr.Item().Text(faculty.Name).FontSize(11).AlignCenter();
                        hdr.Item().Text(dept.Name).FontSize(10).AlignCenter();
                        hdr.Item().Text(term.Name).FontSize(9).AlignCenter();
                        hdr.Item().PaddingTop(4).LineHorizontal(1);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        // Columns: Saat + 5 days
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            for (int i = 0; i < 5; i++) cols.RelativeColumn(3);
                        });

                        // Header row
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                .Text("Saat").FontColor(Colors.White).FontSize(8).Bold();
                            foreach (var day in days)
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                    .AlignCenter().Text(DayOfWeekTurkish(day))
                                    .FontColor(Colors.White).FontSize(8).Bold();
                            }
                        });

                        // Build grid lookup
                        var gridLookup = new Dictionary<(int slot, DayOfWeek day), List<string>>();
                        foreach (var r in deptRows)
                        {
                            string instructor = InstructorDisplay(r.InstructorUserId, r.InstructorTitle);
                            // Hoca atanmamışsa o satır boş yerine atlanır
                            string cellText = string.IsNullOrEmpty(instructor)
                                ? $"{r.CourseCode}-{r.SectionNumber}\n{r.ClassroomName}"
                                : $"{r.CourseCode}-{r.SectionNumber}\n{instructor}\n{r.ClassroomName}";
                            var key = (r.SlotOrder, r.DayOfWeek);
                            if (!gridLookup.ContainsKey(key))
                                gridLookup[key] = new List<string>();
                            gridLookup[key].Add(cellText);
                        }

                        // Data rows
                        for (int s = 0; s < slotOrders.Count; s++)
                        {
                            var (slotOrder, startTime, endTime) = slotOrders[s];
                            string slotLabel = $"{startTime:HH\\:mm}\n{endTime:HH\\:mm}";
                            bool isEven = s % 2 == 0;
                            var rowBg = isEven ? Colors.Grey.Lighten4 : Colors.White;

                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                .Text(slotLabel).FontSize(7).Bold();

                            foreach (var day in days)
                            {
                                var key = (slotOrder, day);
                                var cellBg = gridLookup.ContainsKey(key) ? Colors.Green.Lighten4 : rowBg;
                                var cellText = gridLookup.TryGetValue(key, out var texts)
                                    ? string.Join("\n---\n", texts)
                                    : string.Empty;

                                table.Cell().Background(cellBg).Border(0.5f).Padding(3)
                                    .AlignCenter().Text(cellText).FontSize(7);
                            }
                        }

                        if (slotOrders.Count == 0)
                        {
                            table.Cell().ColumnSpan(6).Padding(10)
                                .AlignCenter().Text("Bu bölüm için programlanmış ders bulunmamaktadır.")
                                .FontSize(9).Italic();
                        }
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"Oluşturulma: {TurkeyNow():dd.MM.yyyy HH:mm}").FontSize(7);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Sayfa ").FontSize(7);
                            x.CurrentPageNumber().FontSize(7);
                            x.Span(" / ").FontSize(7);
                            x.TotalPages().FontSize(7);
                        });
                    });
                });

                // ── Page 2: Course List ────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);

                    page.Header().Column(hdr =>
                    {
                        hdr.Item().Text("ANKARA BİLİM ÜNİVERSİTESİ").FontSize(13).Bold().AlignCenter();
                        hdr.Item().Text($"{faculty.Name} — {dept.Name}").FontSize(11).AlignCenter();
                        hdr.Item().Text($"Ders Listesi — {term.Name}").FontSize(10).AlignCenter();
                        hdr.Item().PaddingTop(4).LineHorizontal(1);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);  // Ders Kodu
                            cols.RelativeColumn(5);  // Ders Adı
                            cols.RelativeColumn(1);  // Şube
                            cols.RelativeColumn(4);  // Öğretim Üyesi
                            cols.RelativeColumn(2);  // Gün
                            cols.RelativeColumn(2);  // Saat
                            cols.RelativeColumn(2);  // Derslik
                            cols.RelativeColumn(1.5f); // Kapasite
                        });

                        table.Header(header =>
                        {
                            var headers = new[] { "Ders Kodu", "Ders Adı", "Şube", "Öğretim Üyesi", "Gün", "Saat", "Derslik", "Kapasite" };
                            foreach (var h in headers)
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                    .Text(h).FontColor(Colors.White).FontSize(8).Bold();
                            }
                        });

                        int rowIdx = 0;
                        foreach (var r in deptRows)
                        {
                            var bg = rowIdx++ % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;
                            string instructor = InstructorDisplay(r.InstructorUserId, r.InstructorTitle);
                            string slotTime = $"{r.StartTime:HH\\:mm}-{r.EndTime:HH\\:mm}";

                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(r.CourseCode).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(r.CourseName).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).AlignCenter().Text(r.SectionNumber.ToString()).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(instructor).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(DayOfWeekTurkish(r.DayOfWeek)).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(slotTime).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(r.ClassroomName).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).AlignCenter().Text(r.Capacity.ToString()).FontSize(8);
                        }

                        if (deptRows.Count == 0)
                        {
                            table.Cell().ColumnSpan(8).Padding(10)
                                .AlignCenter().Text("Listelenecek ders bulunmamaktadır.")
                                .FontSize(9).Italic();
                        }
                    });

                    page.Footer().Row(row =>
                    {
                        int uniqueCourses = deptRows.Select(r => r.CourseCode).Distinct().Count();
                        int uniqueInstructors = deptRows
                            .Where(r => r.InstructorUserId.HasValue)
                            .Select(r => r.InstructorUserId).Distinct().Count();

                        row.RelativeItem().Text($"Toplam: {uniqueCourses} ders, {uniqueInstructors} öğretim üyesi")
                            .FontSize(7);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Sayfa ").FontSize(7);
                            x.CurrentPageNumber().FontSize(7);
                            x.Span(" / ").FontSize(7);
                            x.TotalPages().FontSize(7);
                        });
                    });
                });
            }
        });

        return document.GeneratePdf();
    }

    public async Task<ImportPreviewDto> PreviewImportAsync(int userId, Stream fileStream, int termId, int facultyId, CancellationToken ct = default)
    {
        // Oturum fakülte ve döneme yabancı anahtarla bağlı; var olmayan kimlik kayıtta hata verir.
        if (!await _db.Faculties.AnyAsync(f => f.Id == facultyId, ct))
            throw new ScheduleImportSessionInvalidException("Fakülte bulunamadı.");
        if (!await _db.AcademicTerms.AnyAsync(t => t.Id == termId, ct))
            throw new ScheduleImportSessionInvalidException("Dönem bulunamadı.");

        // Turkish day name → DayOfWeek (normalised: lowercase, no diacritics for resilience)
        var dayMap = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            { "Pazartesi", DayOfWeek.Monday },
            { "Salı",      DayOfWeek.Tuesday },
            { "Sali",      DayOfWeek.Tuesday },
            { "Çarşamba",  DayOfWeek.Wednesday },
            { "Carsamba",  DayOfWeek.Wednesday },
            { "Perşembe",  DayOfWeek.Thursday },
            { "Persembe",  DayOfWeek.Thursday },
            { "Cuma",      DayOfWeek.Friday },
        };

        // Pre-load reference data for the term/faculty to avoid N+1 queries
        var sections = await _db.CourseSections
            .Where(s => s.TermId == termId && s.Course.Department!.FacultyId == facultyId)
            .Include(s => s.Course)
            .Include(s => s.SectionSchedules)
            .ToListAsync(ct);

        var sectionLookup = sections.ToDictionary(
            s => (s.Course.Code, s.SectionNumber),
            s => s);

        var timeSlots = await _db.TimeSlots
            .Where(ts => ts.TermId == termId)
            .ToListAsync(ct);

        var classrooms = await _db.Classrooms.ToListAsync(ct);
        var classroomLookup = classrooms
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Existing schedules (all sections in term/faculty) for conflict detection
        var existingSchedules = await _db.SectionSchedules
            .Where(ss => ss.CourseSection.TermId == termId
                      && ss.CourseSection.Course.Department!.FacultyId == facultyId)
            .Include(ss => ss.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook(fileStream);
        ValidateWorkbookShape(workbook);

        var sheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Program Verisi")
                    ?? workbook.Worksheet(1);

        var serverRows = new List<ScheduleImportSessionRow>();
        var importedSchedules = new List<(int SectionId, int TimeSlotId, int ClassroomId, int RowNumber)>();

        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            var row = new ScheduleImportSessionRow { RowNumber = r };

            string courseCode     = sheet.Cell(r, 1).GetValue<string>().Trim();
            string sectionCodeRaw = sheet.Cell(r, 3).GetValue<string>().Trim();
            string dayRaw         = sheet.Cell(r, 5).GetValue<string>().Trim();
            string startTimeRaw   = sheet.Cell(r, 6).GetValue<string>().Trim();
            string endTimeRaw     = sheet.Cell(r, 7).GetValue<string>().Trim();
            string classroomName  = sheet.Cell(r, 8).GetValue<string>().Trim();
            string capacityRaw    = sheet.Cell(r, 9).GetValue<string>().Trim();

            // Skip rows with no course code (likely empty)
            if (string.IsNullOrWhiteSpace(courseCode))
                continue;

            row.CourseCode    = courseCode;
            row.SectionCode   = sectionCodeRaw;
            row.Day           = dayRaw;
            row.StartTime     = startTimeRaw;
            row.EndTime       = endTimeRaw;
            row.ClassroomName = classroomName;
            _ = int.TryParse(capacityRaw, out int capacity);
            row.Capacity      = capacity;

            // Skip rows with no day/time (unassigned — no change intended)
            if (string.IsNullOrWhiteSpace(dayRaw) || string.IsNullOrWhiteSpace(startTimeRaw))
            {
                row.Status  = "Skipped";
                row.Message = "Gün veya saat bilgisi yok, atlandı.";
                serverRows.Add(row);
                continue;
            }

            var errors = new List<string>();

            // Validate: section number
            if (!int.TryParse(sectionCodeRaw, out int sectionNumber))
            {
                errors.Add($"Şube kodu geçersiz: '{sectionCodeRaw}'");
            }

            // Validate: course + section exists
            CourseSection? section = null;
            if (errors.Count == 0 && !sectionLookup.TryGetValue((courseCode, sectionNumber), out section))
            {
                errors.Add($"Şube bulunamadı: {courseCode}-{sectionNumber}");
            }

            // Validate: day name
            DayOfWeek dayOfWeek = DayOfWeek.Monday;
            if (!dayMap.TryGetValue(dayRaw, out dayOfWeek))
            {
                errors.Add($"Geçersiz gün adı: '{dayRaw}'");
            }

            // Validate: start/end time parse
            TimeOnly startTime = default, endTime = default;
            if (!TimeOnly.TryParse(startTimeRaw, out startTime))
                errors.Add($"Geçersiz başlangıç saati: '{startTimeRaw}'");
            if (!string.IsNullOrWhiteSpace(endTimeRaw) && !TimeOnly.TryParse(endTimeRaw, out endTime))
                errors.Add($"Geçersiz bitiş saati: '{endTimeRaw}'");

            // Validate: timeslot exists
            TimeSlot? timeSlot = null;
            if (errors.Count == 0)
            {
                // Bitiş saati verilmişse onu da eşleştir — yalnız StartTime eşleşmesi, 09:00-17:00
                // gibi bir satırı 09:00-09:50 slotuna bağlayıp süreyi sessizce kırpıyordu. Export her
                // SectionSchedule için tek slot (tam başlangıç-bitiş) yazdığından tam eşleşme doğrudur.
                var endProvided = endTime != default;
                timeSlot = timeSlots.FirstOrDefault(ts =>
                    ts.DayOfWeek == dayOfWeek &&
                    ts.StartTime == startTime &&
                    (!endProvided || ts.EndTime == endTime));

                if (timeSlot == null)
                    errors.Add(endProvided
                        ? $"Zaman dilimi bulunamadı: {dayRaw} {startTimeRaw}-{endTimeRaw}"
                        : $"Zaman dilimi bulunamadı: {dayRaw} {startTimeRaw}");
            }

            // Validate: classroom exists
            Classroom? classroom = null;
            if (!string.IsNullOrWhiteSpace(classroomName) &&
                !classroomLookup.TryGetValue(classroomName, out classroom))
            {
                errors.Add($"Derslik bulunamadı: '{classroomName}'");
            }
            else if (string.IsNullOrWhiteSpace(classroomName))
            {
                errors.Add("Derslik adı boş olamaz.");
            }

            if (errors.Count > 0)
            {
                row.Status  = "Error";
                row.Message = string.Join("; ", errors);
                serverRows.Add(row);
                continue;
            }

            // At this point all lookups succeeded
            row.SectionId  = section!.Id;
            row.TimeSlotId = timeSlot!.Id;
            row.ClassroomId = classroom!.Id;

            var warnings = new List<string>();

            // Conflict check 1: classroom already used in this timeslot (existing DB schedules)
            bool classroomConflictDb = existingSchedules.Any(ss =>
                ss.ClassroomId == classroom.Id &&
                ss.TimeSlotId  == timeSlot.Id &&
                ss.CourseSection.Id != section.Id);

            // Conflict check 2: classroom used in another import row (within this import batch)
            bool classroomConflictImport = importedSchedules.Any(i =>
                i.ClassroomId == classroom.Id &&
                i.TimeSlotId  == timeSlot.Id &&
                i.SectionId   != section.Id);

            if (classroomConflictDb || classroomConflictImport)
                warnings.Add($"Derslik çakışması: {classroomName} bu saatte başka bir şubede kullanılıyor.");

            // Conflict check 3: instructor conflict
            if (section.InstructorId.HasValue)
            {
                bool instructorConflictDb = existingSchedules.Any(ss =>
                    ss.CourseSection.InstructorId == section.InstructorId &&
                    ss.TimeSlotId == timeSlot.Id &&
                    ss.CourseSection.Id != section.Id);

                bool instructorConflictImport = importedSchedules.Any(i =>
                {
                    var imp = sections.FirstOrDefault(s => s.Id == i.SectionId);
                    return imp?.InstructorId == section.InstructorId &&
                           i.TimeSlotId == timeSlot.Id &&
                           i.SectionId  != section.Id;
                });

                if (instructorConflictDb || instructorConflictImport)
                    warnings.Add("Öğretim üyesi çakışması: Hoca bu saatte başka bir şubede.");
            }

            // Capacity warning
            if (capacity > 0 && classroom.Capacity < capacity)
                warnings.Add($"Kapasite uyarısı: Derslik kapasitesi ({classroom.Capacity}) şube kapasitesinden ({capacity}) küçük.");

            if (warnings.Count > 0)
            {
                row.Status  = "Warning";
                row.Message = string.Join("; ", warnings);
            }
            else
            {
                row.Status  = "Valid";
                row.Message = "Hazır.";
            }

            importedSchedules.Add((section.Id, timeSlot.Id, classroom.Id, r));
            serverRows.Add(row);
        }

        var now = DateTime.UtcNow;
        var session = new ScheduleImportSession
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = userId,
            FacultyId = facultyId,
            TermId = termId,
            RowsJson = JsonSerializer.Serialize(serverRows, SessionJsonOptions),
            CreatedAt = now,
            ExpiresAt = now.Add(ImportSessionLifetime),
        };

        _db.ScheduleImportSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return new ImportPreviewDto
        {
            ImportSessionId = session.Id,
            ExpiresAt = session.ExpiresAt,
            TotalRows = serverRows.Count,
            Rows = serverRows.Select(ToPublicRow).ToList(),
        };
    }

    public async Task<ImportResultDto> ApplyImportAsync(
        int userId,
        int facultyId,
        int termId,
        Guid importSessionId,
        List<int> rowNumbers,
        CancellationToken ct = default)
    {
        if (importSessionId == Guid.Empty)
            throw new ScheduleImportSessionInvalidException("Import session is required.");

        var selectedRowNumbers = rowNumbers.Distinct().ToList();
        if (selectedRowNumbers.Count == 0)
            throw new ScheduleImportSessionInvalidException("At least one row must be selected.");

        IDbContextTransaction? transaction = null;
        try
        {
            if (_db.Database.IsRelational())
            {
                transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            }

            var now = DateTime.UtcNow;
            var session = await _db.ScheduleImportSessions
                .FirstOrDefaultAsync(s => s.Id == importSessionId, ct)
                ?? throw new ScheduleImportSessionInvalidException("Import session was not found.");

            if (session.CreatedByUserId != userId)
                throw new ScheduleImportSessionForbiddenException("Import session belongs to another user.");

            if (session.FacultyId != facultyId || session.TermId != termId)
                throw new ScheduleImportSessionInvalidException("Import session does not match the requested faculty or term.");

            if (session.ConsumedAt.HasValue)
                throw new ScheduleImportSessionInvalidException("Import session has already been used.");

            if (session.ExpiresAt <= now)
                throw new ScheduleImportSessionInvalidException("Import session has expired.");

            var sessionRows = DeserializeSessionRows(session.RowsJson);
            var rowsByNumber = sessionRows.ToDictionary(r => r.RowNumber);
            var rowsToApply = new List<ScheduleImportSessionRow>();

            foreach (var rowNumber in selectedRowNumbers)
            {
                if (!rowsByNumber.TryGetValue(rowNumber, out var row) || !IsApplicable(row))
                {
                    throw new ScheduleImportSessionInvalidException($"Selected row {rowNumber} cannot be applied.");
                }

                rowsToApply.Add(row);
            }

            await ValidateRowsBeforeApplyAsync(rowsToApply, facultyId, termId, ct);

            var sectionIds = rowsToApply
                .Select(r => r.SectionId!.Value)
                .Distinct()
                .ToList();

            var existingSchedules = await _db.SectionSchedules
                .Where(ss => sectionIds.Contains(ss.CourseSectionId))
                .ToListAsync(ct);

            _db.SectionSchedules.RemoveRange(existingSchedules);

            foreach (var row in rowsToApply)
            {
                _db.SectionSchedules.Add(new SectionSchedule
                {
                    CourseSectionId = row.SectionId!.Value,
                    TimeSlotId = row.TimeSlotId!.Value,
                    ClassroomId = row.ClassroomId!.Value,
                });
            }

            session.ConsumedAt = now;
            await _db.SaveChangesAsync(ct);

            if (transaction is not null)
                await transaction.CommitAsync(ct);

            return new ImportResultDto
            {
                AppliedCount = rowsToApply.Count,
            };
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Önizleme ile uygulama arasında başka bir atama yazıldıysa (#33 TOCTOU), DB'nin
            // (ClassroomId, TimeSlotId) unique index'i çift-rezervasyonu reddeder. Serializable
            // transaction geri alınır; ham 500 yerine net, yol gösterici mesaj döndürülür.
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw new ScheduleImportSessionInvalidException(
                "İçe aktarım sırasında derslik/zaman çakışması oluştu (önizlemeden sonra başka bir "
                + "atama yapılmış olabilir). Hiçbir satır uygulanmadı; önizlemeyi yenileyip tekrar deneyin.");
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static List<ScheduleImportSessionRow> DeserializeSessionRows(string rowsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ScheduleImportSessionRow>>(rowsJson, SessionJsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ScheduleImportSessionInvalidException($"Import session data is invalid: {ex.Message}");
        }
    }

    private static bool IsApplicable(ScheduleImportSessionRow row) =>
        (row.Status == "Valid" || row.Status == "Warning")
        && row.SectionId.HasValue
        && row.ClassroomId.HasValue
        && row.TimeSlotId.HasValue;

    private async Task ValidateRowsBeforeApplyAsync(
        List<ScheduleImportSessionRow> rows,
        int facultyId,
        int termId,
        CancellationToken ct)
    {
        var sectionIds = rows.Select(r => r.SectionId!.Value).Distinct().ToList();
        var timeSlotIds = rows.Select(r => r.TimeSlotId!.Value).Distinct().ToList();
        var classroomIds = rows.Select(r => r.ClassroomId!.Value).Distinct().ToList();

        var validSectionIds = await _db.CourseSections
            .Where(s => sectionIds.Contains(s.Id)
                     && s.TermId == termId
                     && s.Course.Department!.FacultyId == facultyId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (validSectionIds.Count != sectionIds.Count)
            throw new ScheduleImportSessionInvalidException("One or more selected sections no longer match the requested faculty or term.");

        var validTimeSlotIds = await _db.TimeSlots
            .Where(ts => timeSlotIds.Contains(ts.Id) && ts.TermId == termId)
            .Select(ts => ts.Id)
            .ToListAsync(ct);

        if (validTimeSlotIds.Count != timeSlotIds.Count)
            throw new ScheduleImportSessionInvalidException("One or more selected time slots no longer match the requested term.");

        var validClassroomIds = await _db.Classrooms
            .Where(c => classroomIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (validClassroomIds.Count != classroomIds.Count)
            throw new ScheduleImportSessionInvalidException("One or more selected classrooms no longer exist.");
    }

    private static ImportRowDto ToPublicRow(ScheduleImportSessionRow row) => new()
    {
        RowNumber = row.RowNumber,
        CourseCode = row.CourseCode,
        SectionCode = row.SectionCode,
        InstructorName = row.InstructorName,
        Day = row.Day,
        StartTime = row.StartTime,
        EndTime = row.EndTime,
        ClassroomName = row.ClassroomName,
        Capacity = row.Capacity,
        Status = row.Status,
        Message = row.Message,
    };

    private static void ValidateWorkbookShape(IXLWorkbook workbook)
    {
        const int maxRows = 5000;
        const int maxColumns = 100;

        foreach (var worksheet in workbook.Worksheets)
        {
            var range = worksheet.RangeUsed();
            if (range is null)
                continue;

            if (range.RowCount() > maxRows || range.ColumnCount() > maxColumns)
                throw new ScheduleImportSessionInvalidException(
                    $"Excel file cannot exceed {maxRows} rows and {maxColumns} columns.");
        }
    }

    private sealed class ScheduleImportSessionRow
    {
        public int RowNumber { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string SectionCode { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string Day { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string ClassroomName { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? SectionId { get; set; }
        public int? ClassroomId { get; set; }
        public int? TimeSlotId { get; set; }
    }

    /// <summary>
    /// Derslik-bazlı pivot sheet'i oluşturur. Satırlarda gün+slot#+saat,
    /// sütunlarda derslikler, hücrelerde ders bilgisi.
    /// Klasik üniversite ders programı (Proliz tarzı) görünümü.
    /// </summary>
    private static void BuildClassroomPivotSheet<TRow, TClassroom, TSlot>(
        XLWorkbook workbook,
        IEnumerable<TRow> rows,
        Dictionary<int, string> userNames,
        IReadOnlyList<TClassroom> classrooms,
        IReadOnlyList<TSlot> allSlots,
        string facultyCode)
        where TRow      : class
        where TClassroom: class
        where TSlot     : class
    {
        T Get<T>(object obj, string prop) => (T)obj.GetType().GetProperty(prop)!.GetValue(obj)!;

        var pivot = workbook.Worksheets.Add("Derslik Bazlı Program", position: 1);

        var days = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
        };

        var classroomList = classrooms.ToList();
        if (classroomList.Count == 0)
        {
            pivot.Cell(1, 1).Value = "Bu fakülteye bu dönemde atanmış derslik yok. Derslik Yönetimi > Fakülte Atama sayfasından derslik atayabilirsiniz.";
            pivot.Range(1, 1, 1, 5).Merge();
            pivot.Columns().AdjustToContents();
            return;
        }

        // ── Renk paleti (ekran görüntüsündeki Proliz tarzı) ─────────────────
        var headerBg     = XLColor.FromArgb(0x1F, 0x49, 0x7D); // koyu lacivert
        var dayBg        = XLColor.FromArgb(0x4F, 0x81, 0xBD); // orta mavi (Pazartesi/Salı etiketi)
        var slotBg       = XLColor.FromArgb(0xDB, 0xE5, 0xF1); // çok açık mavi (slot # ve saat)
        var emptyBg      = XLColor.White;                       // boş hücre — temiz beyaz
        var highlightBg  = XLColor.FromArgb(0xFF, 0xFF, 0x99); // sarı vurgu (parlak)
        var gridBorder   = XLColor.FromArgb(0xBF, 0xBF, 0xBF); // soft gri border
        var summaryBg    = XLColor.FromArgb(0xF2, 0xF2, 0xF2); // alttaki kullanım oranı

        // ── Header satırı 1: [Gün] [Slot#] [Saat] [Derslik 1] [Derslik 2] ──
        var hdrGun = pivot.Cell(1, 1);
        hdrGun.Value = "Gün";
        hdrGun.Style.Font.Bold = true;
        hdrGun.Style.Fill.BackgroundColor = headerBg;
        hdrGun.Style.Font.FontColor = XLColor.White;
        hdrGun.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hdrGun.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var hdrSlot = pivot.Cell(1, 2);
        hdrSlot.Value = "#";
        hdrSlot.Style.Font.Bold = true;
        hdrSlot.Style.Fill.BackgroundColor = headerBg;
        hdrSlot.Style.Font.FontColor = XLColor.White;
        hdrSlot.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hdrSlot.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var hdrSaat = pivot.Cell(1, 3);
        hdrSaat.Value = "Saat";
        hdrSaat.Style.Font.Bold = true;
        hdrSaat.Style.Fill.BackgroundColor = headerBg;
        hdrSaat.Style.Font.FontColor = XLColor.White;
        hdrSaat.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hdrSaat.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        for (int c = 0; c < classroomList.Count; c++)
        {
            var crName  = Get<string>(classroomList[c]!, "Name");
            var crCap   = Get<int>(classroomList[c]!, "Capacity");
            var cell = pivot.Cell(1, c + 4);
            cell.Value = $"{crName} ({crCap} Kişi)";
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = headerBg;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = gridBorder;
        }

        // ── Header satırı 2: Fakülte kodu (MMF/MUH gibi) ────────────────────
        // İlk 3 sütun boş (Gün/#/Saat header'ı tek satır).
        pivot.Cell(2, 1).Style.Fill.BackgroundColor = headerBg;
        pivot.Cell(2, 2).Style.Fill.BackgroundColor = headerBg;
        pivot.Cell(2, 3).Style.Fill.BackgroundColor = headerBg;
        for (int c = 0; c < classroomList.Count; c++)
        {
            var cell = pivot.Cell(2, c + 4);
            cell.Value = facultyCode;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 9;
            cell.Style.Fill.BackgroundColor = headerBg;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = gridBorder;
        }

        // ── Pivot lookup: (day, slotOrder, classroomName) → cellText ───────
        var lookup = new Dictionary<(DayOfWeek, int, string), string>();
        foreach (var r in rows)
        {
            var dow = Get<DayOfWeek>(r, "DayOfWeek");
            var slotOrder = Get<int>(r, "SlotOrder");
            var crName = Get<string>(r, "ClassroomName");
            var courseCode = Get<string>(r, "CourseCode");
            var courseName = Get<string>(r, "CourseName");
            var section = Get<int>(r, "SectionNumber");
            var uid = (int?)r.GetType().GetProperty("InstructorUserId")?.GetValue(r);
            var title = (string)(r.GetType().GetProperty("InstructorTitle")?.GetValue(r) ?? string.Empty);

            string instructorName = uid.HasValue && userNames.TryGetValue(uid.Value, out var name)
                ? (string.IsNullOrWhiteSpace(title) ? name : $"{title} {name}")
                : "-";

            // Format: "KOD-Şube · Ders Adı · Öğretim Üyesi"
            string cellText = $"{courseCode}-{section:00} · {courseName} · {instructorName}";
            lookup[(dow, slotOrder, crName)] = cellText;
        }

        // Aynı dersin birden fazla derslikte aynı saatte olması durumu —
        // bu çakışma sarı vurguyla işaretlenir (Proliz tarzı).
        // Pivot lookup'ta aynı (day, slotOrder) için kaç farklı derslik var?
        var slotMultiClassroomKeys = new HashSet<(DayOfWeek, int)>();
        var slotClassroomCount = new Dictionary<(DayOfWeek, int), int>();
        foreach (var key in lookup.Keys)
        {
            var slotKey = (key.Item1, key.Item2);
            slotClassroomCount[slotKey] = slotClassroomCount.GetValueOrDefault(slotKey) + 1;
        }
        foreach (var (k, v) in slotClassroomCount)
        {
            if (v > 1) slotMultiClassroomKeys.Add(k);
        }

        // ── Slot'ları güne göre grupla ──────────────────────────────────────
        var slotsByDay = allSlots
            .GroupBy(s => Get<DayOfWeek>(s, "DayOfWeek"))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => Get<int>(s, "SlotOrder")).ToList());

        // Derslik başına dolu slot sayısı (kullanım oranı için)
        var classroomFilledCount = new int[classroomList.Count];
        int totalSlotRowsForUsage = 0;

        int rowIdx = 3; // 1. header, 2. fakülte kodu, 3. ilk data satırı
        foreach (var day in days)
        {
            if (!slotsByDay.TryGetValue(day, out var daySlots) || daySlots.Count == 0)
                continue;

            int dayRowStart = rowIdx;

            for (int sIdx = 0; sIdx < daySlots.Count; sIdx++)
            {
                var slot = daySlots[sIdx];
                var slotOrder = Get<int>(slot, "SlotOrder");
                var startTime = Get<TimeOnly>(slot, "StartTime");
                var endTime   = Get<TimeOnly>(slot, "EndTime");

                // Slot # sütunu (1, 2, 3, ...)
                var slotNumCell = pivot.Cell(rowIdx, 2);
                slotNumCell.Value = sIdx + 1;
                slotNumCell.Style.Fill.BackgroundColor = slotBg;
                slotNumCell.Style.Font.Bold = true;
                slotNumCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                slotNumCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                slotNumCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                slotNumCell.Style.Border.OutsideBorderColor = gridBorder;

                // Saat sütunu (HH:mm-HH:mm)
                var slotCell = pivot.Cell(rowIdx, 3);
                slotCell.Value = $"{startTime:HH\\:mm}-{endTime:HH\\:mm}";
                slotCell.Style.Fill.BackgroundColor = slotBg;
                slotCell.Style.Font.Bold = true;
                slotCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                slotCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                slotCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                slotCell.Style.Border.OutsideBorderColor = gridBorder;

                bool isMultiClassroom = slotMultiClassroomKeys.Contains((day, slotOrder));
                totalSlotRowsForUsage++;

                // Her derslik sütunu
                for (int c = 0; c < classroomList.Count; c++)
                {
                    var crName = Get<string>(classroomList[c]!, "Name");
                    var dataCell = pivot.Cell(rowIdx, c + 4);

                    if (lookup.TryGetValue((day, slotOrder, crName), out var text))
                    {
                        dataCell.Value = text;
                        // Aynı saatte birden fazla derslikte ders varsa sarı vurgu
                        // (paralel dersler — admin'in dikkatini çekmek için).
                        dataCell.Style.Fill.BackgroundColor = isMultiClassroom ? highlightBg : emptyBg;
                        dataCell.Style.Font.FontSize = 9;
                        classroomFilledCount[c]++;
                    }
                    else
                    {
                        dataCell.Style.Fill.BackgroundColor = emptyBg;
                    }

                    dataCell.Style.Alignment.WrapText = true;
                    dataCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    dataCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    dataCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataCell.Style.Border.OutsideBorderColor = gridBorder;
                }

                rowIdx++;
            }

            // Gün sütununu birleştir + stilini ver
            if (rowIdx - 1 > dayRowStart)
            {
                pivot.Range(dayRowStart, 1, rowIdx - 1, 1).Merge();
            }
            var dayCell = pivot.Cell(dayRowStart, 1);
            dayCell.Value = DayOfWeekTurkish(day);
            dayCell.Style.Font.Bold = true;
            dayCell.Style.Font.FontColor = XLColor.White;
            dayCell.Style.Fill.BackgroundColor = dayBg;
            dayCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            dayCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            dayCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dayCell.Style.Border.OutsideBorderColor = gridBorder;

            // Gün arası kalın ayırıcı (görsel olarak gün gruplarını ayır)
            var separator = pivot.Range(rowIdx - 1, 1, rowIdx - 1, classroomList.Count + 3);
            separator.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            separator.Style.Border.BottomBorderColor = XLColor.FromArgb(0x6B, 0x7C, 0x93);
        }

        // ── Alt satır: "Derslik Kullanım Oranı" ──────────────────────────────
        var usageRow = rowIdx;
        var usageLabel = pivot.Cell(usageRow, 1);
        usageLabel.Value = "Derslik Kullanım Oranı";
        usageLabel.Style.Font.Bold = true;
        usageLabel.Style.Fill.BackgroundColor = summaryBg;
        usageLabel.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        usageLabel.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        pivot.Range(usageRow, 1, usageRow, 3).Merge();
        pivot.Range(usageRow, 1, usageRow, 3).Style.Fill.BackgroundColor = summaryBg;
        pivot.Range(usageRow, 1, usageRow, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        pivot.Range(usageRow, 1, usageRow, 3).Style.Border.OutsideBorderColor = gridBorder;

        for (int c = 0; c < classroomList.Count; c++)
        {
            var pct = totalSlotRowsForUsage > 0
                ? (double)classroomFilledCount[c] / totalSlotRowsForUsage * 100.0
                : 0.0;
            var cell = pivot.Cell(usageRow, c + 4);
            cell.Value = pct / 100.0;  // Excel % formatı için ondalık
            cell.Style.NumberFormat.Format = "0.00%";
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = summaryBg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = gridBorder;
        }

        // ── Sütun genişlikleri ──────────────────────────────────────────────
        pivot.Column(1).Width = 12;  // Gün
        pivot.Column(2).Width = 5;   // Slot #
        pivot.Column(3).Width = 13;  // Saat
        for (int c = 0; c < classroomList.Count; c++)
        {
            pivot.Column(c + 4).Width = 32;  // Derslikler — geniş, içerik sığsın
        }

        // Satır yükseklikleri
        pivot.Row(1).Height = 24;  // Üst header
        pivot.Row(2).Height = 16;  // Fakülte kodu alt başlığı
        for (int r = 3; r < rowIdx; r++)
        {
            pivot.Row(r).Height = 22;
        }
        pivot.Row(usageRow).Height = 22;

        // Donma: ilk 2 header satır + ilk 3 sütun (Gün/Slot#/Saat)
        pivot.SheetView.FreezeRows(2);
        pivot.SheetView.FreezeColumns(3);

        // ── Yazdırma ayarları ────────────────────────────────────────────────
        pivot.PageSetup.PaperSize = XLPaperSize.A3Paper;
        pivot.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        pivot.PageSetup.Margins.Top = 0.5;
        pivot.PageSetup.Margins.Bottom = 0.5;
        pivot.PageSetup.Margins.Left = 0.3;
        pivot.PageSetup.Margins.Right = 0.3;
        pivot.PageSetup.FitToPages(1, 0);
    }

    private static string DayOfWeekTurkish(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday    => "Pazartesi",
        DayOfWeek.Tuesday   => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday  => "Perşembe",
        DayOfWeek.Friday    => "Cuma",
        DayOfWeek.Saturday  => "Cumartesi",
        DayOfWeek.Sunday    => "Pazar",
        _                   => day.ToString()
    };

    /// <summary>
    /// Export "Oluşturulma" damgası için Türkiye yerel saati. Sistem her yerde UtcNow tutuyor;
    /// DateTime.Now sunucu TZ'sine bağlıydı (UTC sunucuda yanlış saat gösterirdi). Türkiye sabit
    /// UTC+3 (2016'dan beri DST yok) → UtcNow + 3 saat, sunucu TZ'sinden bağımsız doğru sonuç.
    /// </summary>
    private static DateTime TurkeyNow() => DateTime.UtcNow.AddHours(3);

    // ── Ders kartı renkleri (Haftalık Grid sheet için) ─────────────────────
    // Excel hücre background renkleri olarak kullanılır. Ders kodu prefix'ine göre
    // (CENG, SENG, MATH, vb.) sabit renk atanır → aynı bölümün dersleri aynı renk,
    // bölümler birbirinden ayırt edilebilir. Pastel tonlar seçilmiş ki üstüne yazılan
    // siyah metin okunabilir kalsın.
    private static readonly Dictionary<string, (int r, int g, int b)> CourseCardColors = new()
    {
        // Mühendislik ana bölümleri
        { "CENG", (0xDB, 0xE7, 0xF5) },  // soft blue
        { "BM",   (0xDB, 0xE7, 0xF5) },  // soft blue (BM = Bilgisayar Müh.)
        { "SENG", (0xE9, 0xDD, 0xF2) },  // soft purple
        { "ISE",  (0xE9, 0xDD, 0xF2) },  // soft purple
        { "IE",   (0xFE, 0xF2, 0xC8) },  // soft yellow
        { "EEE",  (0xD9, 0xEC, 0xF0) },  // soft cyan
        // Temel bilimler
        { "MATH", (0xFC, 0xE4, 0xD4) },  // soft orange (matematik)
        { "MAT",  (0xFC, 0xE4, 0xD4) },  // soft orange
        { "PHY",  (0xE4, 0xF1, 0xD9) },  // soft green (fizik)
        { "FIZ",  (0xE4, 0xF1, 0xD9) },  // soft green
        { "BIO",  (0xFB, 0xD9, 0xD9) },  // soft pink (biyoloji)
        { "CHEM", (0xFB, 0xD9, 0xD9) },  // soft pink
        { "KIM",  (0xFB, 0xD9, 0xD9) },  // soft pink
        // Dil ve sosyal
        { "ENG",  (0xE0, 0xE8, 0xE0) },  // soft sage
        { "TURK", (0xF5, 0xE6, 0xD9) },  // soft beige
        { "HIST", (0xF5, 0xE6, 0xD9) },  // soft beige
        { "TAR",  (0xF5, 0xE6, 0xD9) },  // soft beige
        // İSG, OHS ve diğer ortak dersler
        { "OHS",  (0xF1, 0xE2, 0xCB) },  // soft tan
        { "ISG",  (0xF1, 0xE2, 0xCB) },  // soft tan
        { "TE",   (0xE6, 0xE0, 0xF5) },  // soft lavender (Teknik Elektif)
    };

    // 16-renk fallback paleti — prefix dictionary'de bulunmayan ders kodları için
    // hash-bazlı tutarlı renk ataması. Frontend CalendarGrid paletiyle aynı tonlarda.
    private static readonly (int r, int g, int b)[] FallbackCardPalette = new[]
    {
        (0xDB, 0xE7, 0xF5),  (0xE4, 0xF1, 0xD9),  (0xE9, 0xDD, 0xF2),  (0xFC, 0xE4, 0xD4),
        (0xFB, 0xD9, 0xD9),  (0xD9, 0xEC, 0xF0),  (0xF5, 0xE6, 0xD9),  (0xE6, 0xE0, 0xF5),
        (0xE0, 0xE8, 0xE0),  (0xF5, 0xDB, 0xE7),  (0xFE, 0xF2, 0xC8),  (0xF1, 0xE2, 0xCB),
        (0xDD, 0xF0, 0xE5),  (0xF0, 0xDD, 0xE0),  (0xFE, 0xF1, 0xD6),  (0xE5, 0xDD, 0xEF),
    };

    /// <summary>
    /// Ders kodundan harf prefix'ini çıkarıp sabit renk döner. Prefix dictionary'de
    /// yoksa kod hash'inden 16-renk fallback paletinden seçer (deterministik).
    /// </summary>
    private static (int r, int g, int b) GetCourseCardColor(string courseCodeWithSection)
    {
        // "CENG 213-1" → "CENG", "MATH101-2" → "MATH"
        string prefix = "";
        foreach (var ch in courseCodeWithSection)
        {
            if (char.IsLetter(ch)) prefix += ch;
            else break;
        }

        if (!string.IsNullOrEmpty(prefix) && CourseCardColors.TryGetValue(prefix.ToUpperInvariant(), out var color))
            return color;

        // Deterministik hash → 16-renk paletinden tutarlı seçim
        int hash = Math.Abs(courseCodeWithSection.GetHashCode());
        return FallbackCardPalette[hash % FallbackCardPalette.Length];
    }
}
