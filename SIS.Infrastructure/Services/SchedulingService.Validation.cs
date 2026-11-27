using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService'in validate (manuel atama / mevcut program çakışma) bölümü.
/// Auto-schedule motoru AutoScheduleEngine'da; bu dosya manuel akış için.
/// </summary>
public partial class SchedulingService
{
    /// <summary>
    /// N aday hücreyi tek HTTP çağrısında değerlendirir. Düzenleyici drag-start'ta tüm
    /// boş hücreler için per-cell {isValid, reason} döndürür. Snapshot/config/state yalnız
    /// bir kez yüklenir; her aday sadece BuildCandidateRuntime + RuleEngine maliyeti yaşar.
    /// </summary>
    public async Task<BulkValidateCellsResultDto> ValidateCellsBulkAsync(
        BulkValidateCellsRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = new BulkValidateCellsResultDto();

        if (request.SectionId <= 0 || request.Candidates.Count == 0)
        {
            return result;
        }

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            // Bilinmeyen section → tüm adaylar invalid olarak işaretle.
            foreach (var c in request.Candidates)
            {
                result.Results.Add(new BulkValidateCellResultDto
                {
                    CellKey = c.CellKey,
                    IsValid = false,
                    Reason = "Section bulunamadı."
                });
            }
            return result;
        }

        // Savunma katmanı: Pool drag-start akışında frontend section'ın atanmış classroomId'sini
        // bilmez (henüz atanmamış section). Bu durumda request.ClassroomId null gelir; backend
        // null classroom için H4 ClassroomType kuralını skip eder → tüm hücreler yanlışlıkla
        // yeşil gözükür → drop sırasında frontend tip-uygun classroom seçer ama bulk validate
        // yanlış classroom'u test etmiş olur. Çözüm: classroomId null geldiğinde section type'a
        // göre uygun ilk classroom'u seçip her aday'a inject et — bulk validate'in test ettiği
        // classroom drop'ta seçilenle tip olarak eşleşir.
        if (request.ClassroomId == null && Scheduling.TeachingMethodPolicy.RequiresClassroom(section.Course.TeachingMethod))
        {
            // Assignment.cs:60-61 ile birebir aynı türetim: pür-lab dersler Lab tipi bekler, diğerleri Lecture.
            var expectedType = section.Course.LabHours > 0 && section.Course.TheoryHours == 0
                ? ClassroomType.Lab : ClassroomType.Lecture;

            // Temsili oda yalnız tip değil, section'ın FAKÜLTESİNE bu dönem atanmış derslikler
            // arasından seçilir → bulk-validate'in test ettiği oda, drop'ta önerilen (faculty-scoped,
            // #18) odalarla tutarlı olur. Aksi halde başka fakültenin odası test edilip hücre yanlış
            // yeşil/kırmızı gösterebilirdi. (Ortak ders → DepartmentId null → faculty filtresi yok.)
            var fallbackQuery = _context.Classrooms
                .AsNoTracking()
                .Where(c => c.Type == expectedType && c.Capacity >= section.Capacity);

            var sectionFacultyId = section.Course.DepartmentId.HasValue
                ? await _context.Departments.AsNoTracking()
                    .Where(d => d.Id == section.Course.DepartmentId.Value)
                    .Select(d => (int?)d.FacultyId)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            if (sectionFacultyId.HasValue)
            {
                var assignedIds = _context.ClassroomTermAssignments
                    .Where(cta => cta.TermId == section.TermId && cta.FacultyId == sectionFacultyId.Value)
                    .Select(cta => cta.ClassroomId);
                fallbackQuery = fallbackQuery.Where(c => assignedIds.Contains(c.Id));
            }

            var fallbackClassroomId = await fallbackQuery
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (fallbackClassroomId.HasValue)
            {
                request.ClassroomId = fallbackClassroomId;
                foreach (var c in request.Candidates)
                {
                    foreach (var slot in c.ScheduleSlots)
                    {
                        slot.ClassroomId ??= fallbackClassroomId;
                    }
                }
            }
        }

        // Ortak ağır işler: snapshot + config + state — yalnız bir kez.
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);
        var runtimeSnapshot = await BuildRuntimeSnapshotAsync(
            section.TermId, section.Id, null, cancellationToken);
        var fullCfg = BuildConstraintConfig(softConfig, hardConfig);

        foreach (var candidate in request.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var (runtimeCandidate, _) = await BuildCandidateRuntimeAsync(
                    section,
                    request.InstructorId,
                    request.ClassroomId,
                    candidate.ScheduleSlots,
                    cancellationToken);

                // State candidate'in InstructorId/DepartmentId'sine göre yüklenir;
                // bu nedenle her aday için ayrı state (instructor değişebilir).
                var state = await _constraintStateLoader.LoadConstraintStateAsync(
                    runtimeCandidate.InstructorId,
                    runtimeCandidate.DepartmentId,
                    runtimeCandidate.SectionId,
                    section.TermId,
                    cancellationToken);
                fullCfg.ApplyConstraintStateOverrides(state);

                var conflicts = await _ruleEngine.EvaluateCandidateAsync(
                    runtimeCandidate, runtimeSnapshot, fullCfg, state, cancellationToken);

                result.Results.Add(new BulkValidateCellResultDto
                {
                    CellKey = candidate.CellKey,
                    IsValid = conflicts.Count == 0,
                    Reason = conflicts.Count > 0 ? conflicts[0].Message : null
                });
            }
            catch (Exception ex)
            {
                // Tek aday başarısız olursa diğerleri etkilenmesin — onu invalid işaretle, gerekçe ex.Message.
                result.Results.Add(new BulkValidateCellResultDto
                {
                    CellKey = candidate.CellKey,
                    IsValid = false,
                    Reason = ex.Message
                });
            }
        }

        return result;
    }

    public Task<ScheduleValidationResultDto> ValidateAsync(ScheduleValidationRequestDto request, CancellationToken cancellationToken = default)
        => ValidateCandidateAsync(request, partner: null, cancellationToken);

    public async Task<(ScheduleValidationResultDto First, ScheduleValidationResultDto Second)> ValidateSwapAsync(
        ScheduleValidationRequestDto first, ScheduleValidationRequestDto second, CancellationToken cancellationToken = default)
        => (await ValidateCandidateAsync(first, second, cancellationToken), await ValidateCandidateAsync(second, first, cancellationToken));

    /// <summary>
    /// Aday şubeyi dönemin anlık görüntüsüne karşı denetler. <paramref name="partner"/> verilirse (takas) ortak şube anlık görüntüde
    /// veritabanındaki konumuyla değil, isteğin tarif ettiği son konumuyla yer alır; kimliği ve diğer bilgileri değişmez.
    /// </summary>
    private async Task<ScheduleValidationResultDto> ValidateCandidateAsync(
        ScheduleValidationRequestDto request, ScheduleValidationRequestDto? partner, CancellationToken cancellationToken)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };

        if (request.SectionId <= 0)
        {
            result.IsValid = false;
            result.Notes.Add("Geçerli bir section ID gönderilmelidir.");
            return result;
        }

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            result.IsValid = false;
            result.Notes.Add("Section bulunamadı.");
            return result;
        }

        var (candidate, candidateNotes) = await BuildCandidateRuntimeAsync(
            section, request.InstructorId, request.ClassroomId, request.ScheduleSlots, cancellationToken);
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);
        var constraintState = await _constraintStateLoader.LoadConstraintStateAsync(
            candidate.InstructorId, candidate.DepartmentId, candidate.SectionId, section.TermId, cancellationToken);

        // H4 alt-toggle'ları H4.Parameters JSON'undan oku (2026-05-19 dual-channel temizliği).
        // fullCfg aşağıda BuildConstraintConfig ile kurulduktan sonra state ile zenginleştirilir.

        Dictionary<int, RuntimeCandidate>? replacements = null;
        if (partner != null)
        {
            var partnerSection = await _context.CourseSections
                .AsNoTracking()
                .Include(cs => cs.Course)
                .Include(cs => cs.Instructor)
                .Include(cs => cs.Classroom)
                .FirstOrDefaultAsync(cs => cs.Id == partner.SectionId, cancellationToken);
            if (partnerSection != null)
            {
                var (partnerCandidate, _) = await BuildCandidateRuntimeAsync(
                    partnerSection, partner.InstructorId, partner.ClassroomId, partner.ScheduleSlots, cancellationToken);
                replacements = new Dictionary<int, RuntimeCandidate> { [partnerSection.Id] = partnerCandidate };
            }
        }

        var runtimeSnapshot = await BuildRuntimeSnapshotAsync(
            section.TermId, section.Id, null, cancellationToken, replacements);

        result.Notes.Add($"Degerlendirilen donem: {runtimeSnapshot.TermName}");
        result.Notes.AddRange(candidateNotes);

        var fullCfg = BuildConstraintConfig(softConfig, hardConfig);
        fullCfg.ApplyConstraintStateOverrides(constraintState);

        var ruleConflicts = await _ruleEngine.EvaluateCandidateAsync(
            candidate, runtimeSnapshot, fullCfg, constraintState, cancellationToken);

        result.Conflicts = ruleConflicts.ToList();
        result.IsValid = ruleConflicts.Count == 0;
        return result;
    }

    public async Task<ScheduleValidationResultDto> ValidateExistingAsync(int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };
        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);

        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        var snapshot = await BuildRuntimeSnapshotAsync(resolvedTerm.Id, null, null, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);

        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        return evaluation;
    }

    public async Task<ScheduleValidationResultDto> ValidateExistingByDepartmentAsync(int departmentId, int? termId = null, CancellationToken cancellationToken = default)
    {
        var result = new ScheduleValidationResultDto { IsValid = true };
        if (departmentId <= 0)
        {
            result.IsValid = false;
            result.Notes.Add("Geçerli bir bölüm ID gönderilmelidir.");
            return result;
        }

        var resolvedTerm = await ResolveTermAsync(termId, cancellationToken);
        if (resolvedTerm == null)
        {
            result.IsValid = false;
            result.Notes.Add(termId.HasValue
                ? "Belirtilen akademik dönem bulunamadı."
                : "Aktif akademik dönem bulunamadı.");
            return result;
        }

        var snapshot = await BuildRuntimeSnapshotAsync(resolvedTerm.Id, null, departmentId, cancellationToken);
        var evaluation = await EvaluateExistingConflictsAsync(snapshot, cancellationToken);
        evaluation.Notes.Insert(0, $"Degerlendirilen donem: {snapshot.TermName}");
        evaluation.Notes.Insert(1, $"Kapsam: {snapshot.ScopeName}");
        return evaluation;
    }

    private async Task<ScheduleValidationResultDto> EvaluateExistingConflictsAsync(RuntimeSnapshot snapshot, CancellationToken ct)
    {
        var softConfig = await LoadSoftConstraintConfigAsync(ct);
        var hardConfig = await LoadHardConstraintConfigAsync(ct);
        var config = BuildConstraintConfig(softConfig, hardConfig);
        var state = await _constraintStateLoader.LoadConstraintStateAsync(null, null, null, snapshot.TermId, ct);
        config.ApplyConstraintStateOverrides(state);

        // Plan 12 Faz 5 — Hard + soft validation RuleEngine üzerinden. Soft note'lar
        // EvaluateWithBreakdownAsync ConstraintScoreItemDto.SampleViolations'tan toplanır.
        var hardConflicts = await _ruleEngine.EvaluateAllExistingAsync(snapshot, config, state, ct);
        var breakdown = await _ruleEngine.EvaluateWithBreakdownAsync(snapshot, config, state, ct);

        var softNotes = breakdown.Constraints
            .Where(item => item.Category == "Soft")
            .SelectMany(item => item.SampleViolations)
            .ToList();

        return new ScheduleValidationResultDto
        {
            IsValid = hardConflicts.Count == 0,
            Conflicts = hardConflicts
                .OrderBy(conflict => conflict.SectionId)
                .ThenBy(conflict => conflict.ConflictType)
                .ThenBy(conflict => conflict.Day)
                .ThenBy(conflict => conflict.StartTime)
                .ToList(),
            Notes = softNotes.Distinct().OrderBy(note => note).ToList()
        };
    }

    private async Task<(List<SchedulingConflictDto> Conflicts, List<string> SoftNotes)> EvaluateCandidateConflictsAsync(
        RuntimeCandidate candidate,
        RuntimeSnapshot snapshot,
        SoftConstraintConfig softConfig,
        HardConstraintConfig? hardConfig,
        ConstraintState? state,
        CancellationToken ct)
    {
        // Plan 12 Faz 7 — V1 ConstraintEvaluationService.EvaluateCandidate yerine RuleEngine
        // delegasyonu. SuggestAsync path'i için kullanılır (manuel slot önerisi).
        var config = BuildConstraintConfig(softConfig, hardConfig);
        config.ApplyConstraintStateOverrides(state);

        var conflicts = (await _ruleEngine.EvaluateCandidateAsync(candidate, snapshot, config, state, ct)).ToList();

        // Soft notes — SuggestAsync feasibility için sadece bilgilendirici, ihlal değil.
        // CalculatePenaltyAsync skalar dönüyor; tek candidate için detaylı breakdown
        // SuggestAsync hızı için skipped (UI'da kabul edilebilir trade).
        return (conflicts, new List<string>());
    }
}
