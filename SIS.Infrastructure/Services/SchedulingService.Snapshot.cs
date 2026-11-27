using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Scheduling.Constraints;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Rules;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService'in <see cref="RuntimeSnapshot"/> / <see cref="RuntimeCandidate"/>
/// inşa eden partial bölümü. Auto-schedule motoru ile ApplyScheduleAsync (manuel atama)
/// AYNI snapshot yapısını kullanır; bu dosya ortak inşa kodunu izole eder.
///
/// Method'lar:
///   - BuildRuntimeSnapshotAsync          (term-scope read-only snapshot, ConflictPairsBuilder integrated)
///   - BuildExistingRuntimeCandidate      (DB'deki section'dan RuntimeCandidate)
///   - BuildCandidateRuntimeAsync         (validate için aday section, notes ayrı tuple)
///   - BuildCandidateRuntime              (saf inşa, IO yok)
/// </summary>
public partial class SchedulingService
{
    internal async Task<RuntimeSnapshot> BuildRuntimeSnapshotAsync(
        int termId,
        int? excludedSectionId,
        int? departmentId,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<int, RuntimeCandidate>? replacementCandidates = null)
    {
        var sections = await LoadActiveSectionsAsync(termId, excludedSectionId, departmentId, cancellationToken);
        var weeklyHoursPolicy = await InstructorWeeklyHoursPolicy.LoadAsync(_systemConfig, cancellationToken);
        foreach (var section in sections)
        {
            if (section.Instructor != null)
                section.Instructor.MaxWeeklyHours = weeklyHoursPolicy.Resolve(section.Instructor.Title, section.Instructor.MaxWeeklyHours);
        }
        var (termName, scopeName) = await LoadTermAndScopeNamesAsync(termId, departmentId, cancellationToken);

        var instructorIds = sections
            .Where(section => section.InstructorId.HasValue)
            .Select(section => section.InstructorId!.Value)
            .Distinct()
            .ToList();
        var instructorNameMap = await LoadInstructorNameMapAsync(instructorIds, cancellationToken);
        var courseMetadata = await LoadCourseSchedulingMetadataAsync(
            termId, sections.Select(section => section.CourseId), cancellationToken);

        var (classroomAssignments, deptFacultyMap) = await LoadFacultyClassroomDataAsync(termId, cancellationToken);
        var corequisites = await LoadCorequisitePairsAsync(cancellationToken);

        var softCfg = await LoadSoftConstraintConfigAsync(cancellationToken);
        var coEnrollmentRuntimePairs = await LoadCoEnrollmentRuntimePairsAsync(termId, softCfg, cancellationToken);

        var distributionConstraints = await LoadDistributionConstraintsAsync(termId, cancellationToken);
        var slotAllowedModes = await LoadSlotAllowedModesAsync(termId, cancellationToken);

        var candidates = ProjectRuntimeCandidates(sections, instructorNameMap, courseMetadata);
        // Takas doğrulaması: ortak şube veritabanındaki konumu yerine takas sonrası adayıyla yer alır (SectionId aynı).
        if (replacementCandidates != null)
            candidates = candidates.Select(c => replacementCandidates.GetValueOrDefault(c.SectionId, c)).ToList();

        var rawConfig = await LoadSystemConfigAsync(cancellationToken);
        var fullCfg = SchedulingConstraintConfig.FromSystemConfig(rawConfig);
        // 2026-05-31 — CurriculumConflictMode tek kaynak: H5.Parameters JSON (solver ile aynı).
        // FromSystemConfig default "curriculum" döndürür; ApplyConstraintStateOverrides H5.Parameters'taki
        // gerçek modu ("cohort") uygular. Aksi halde evaluator "curriculum", solver "cohort" çalışıp drift
        // oluşur → panel, solver'ın bilerek izin verdiği (modulo) çakışmaları sahte uyarı olarak gösterir.
        var constraintState = await _constraintStateLoader.LoadConstraintStateAsync(
            null, null, null, termId, cancellationToken);
        fullCfg.ApplyConstraintStateOverrides(constraintState);
        var curriculumConflictMode = fullCfg.CurriculumConflictMode;

        var conflictData = await BuildConflictPairsAsync(
            candidates, courseMetadata, curriculumConflictMode,
            fullCfg.EnrollmentConflictHardThreshold, termId, cancellationToken);

        return new RuntimeSnapshot
        {
            TermId = termId,
            TermName = termName,
            ScopeName = scopeName,
            Sections = candidates,
            ClassroomAssignments = classroomAssignments,
            DeptFacultyMap = deptFacultyMap,
            Corequisites = corequisites,
            SlotAllowedModes = slotAllowedModes,
            CohortConflictPairs = conflictData.CohortConflictPairs ?? new HashSet<(int, int)>(),
            CurriculumConflictMode = curriculumConflictMode,
            EnrollmentConflicts = conflictData.EnrollmentConflicts,
            CoEnrollmentPairs = coEnrollmentRuntimePairs,
            AdjacentYearSoftPairs = conflictData.AdjacentYearSoftPairs ?? new HashSet<(int, int)>(),
            DistributionConstraints = distributionConstraints,
        };
    }

    private async Task<List<CourseSection>> LoadActiveSectionsAsync(
        int termId, int? excludedSectionId, int? departmentId, CancellationToken ct)
    {
        var query = _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .Include(cs => cs.SectionSchedules).ThenInclude(ss => ss.TimeSlot)
            .Where(cs => cs.TermId == termId && cs.Status == CourseSectionStatus.Active)
            .Where(cs => !excludedSectionId.HasValue || cs.Id != excludedSectionId.Value);

        if (departmentId.HasValue)
        {
            query = query.Where(cs =>
                cs.Course.DepartmentId == departmentId.Value
                || (cs.Instructor != null && cs.Instructor.DepartmentId == departmentId.Value));
        }

        return await query.ToListAsync(ct);
    }

    private async Task<(string TermName, string ScopeName)> LoadTermAndScopeNamesAsync(
        int termId, int? departmentId, CancellationToken ct)
    {
        var termName = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => t.Id == termId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var scopeName = departmentId.HasValue
            ? await _context.Departments
                .AsNoTracking()
                .Where(d => d.Id == departmentId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct) ?? "Bolum"
            : "Tum sistem";

        return (termName, scopeName);
    }

    /// <summary>H10 — ClassroomTermAssignment listesi + dept→faculty map.</summary>
    private async Task<(List<ClassroomTermAssignmentInfo> Assignments, Dictionary<int, int> DeptFacultyMap)>
        LoadFacultyClassroomDataAsync(int termId, CancellationToken ct)
    {
        var entities = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == termId)
            .ToListAsync(ct);

        var assignments = entities
            .Select(cta => new ClassroomTermAssignmentInfo(cta.ClassroomId, cta.FacultyId))
            .ToList();

        var deptFacultyMap = await _context.Departments
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.FacultyId, ct);

        return (assignments, deptFacultyMap);
    }

    /// <summary>H11 — Corequisite course çiftleri.</summary>
    private async Task<List<CorequisitePair>> LoadCorequisitePairsAsync(CancellationToken ct) =>
        await _context.CoursePrerequisites
            .AsNoTracking()
            .Where(cp => cp.IsCorequisite)
            .Select(cp => new CorequisitePair(cp.CourseId, cp.RequiredCourseId))
            .ToListAsync(ct);

    /// <summary>S4 — Co-enrollment pairs (config-aware: S4 kapalıysa boş döner).</summary>
    private async Task<List<CoEnrollmentRuntimePair>> LoadCoEnrollmentRuntimePairsAsync(
        int termId, SoftConstraintConfig softCfg, CancellationToken ct)
    {
        if (!softCfg.S4Enabled)
            return new List<CoEnrollmentRuntimePair>();

        var rawPairs = await LoadCoEnrollmentPairsAsync(termId, softCfg.S4MinCoPairThreshold, ct);
        return rawPairs
            .Select(p => new CoEnrollmentRuntimePair(p.CourseId1, p.CourseId2, p.StudentCount))
            .ToList();
    }

    /// <summary>DIST — Distribution constraints (DistributionType + PreferenceLevel + Courses).</summary>
    private async Task<List<RuntimeDistributionConstraint>> LoadDistributionConstraintsAsync(
        int termId, CancellationToken ct)
    {
        var entities = await _context.DistributionConstraints
            .AsNoTracking()
            .Include(d => d.Courses)
            .Where(d => d.TermId == termId)
            .ToListAsync(ct);

        return entities
            .Select(dc => new RuntimeDistributionConstraint(
                DistributionType: dc.DistributionType,
                PreferenceLevel: dc.PreferenceLevel,
                Courses: dc.Courses
                    .OrderBy(c => c.SortOrder)
                    .Select(c => new DistributionConstraintCourseInfo(c.CourseId, c.CourseSectionId, c.SortOrder))
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// H17 — FaceToFaceSlotRestriction için (Day, StartTime) → AllowedMode lookup.
    /// Duplicate (Day, StartTime) için OnlineOnly daha kısıtlayıcıdır → onu tercih et.
    /// </summary>
    private async Task<Dictionary<(int Day, TimeOnly StartTime), TimeSlotAllowedMode>>
        LoadSlotAllowedModesAsync(int termId, CancellationToken ct)
    {
        var slotRecords = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == termId)
            .Select(ts => new { ts.DayOfWeek, ts.StartTime, ts.AllowedMode })
            .ToListAsync(ct);

        var result = new Dictionary<(int Day, TimeOnly StartTime), TimeSlotAllowedMode>();
        foreach (var slot in slotRecords)
        {
            var key = ((int)slot.DayOfWeek, slot.StartTime);
            if (!result.TryGetValue(key, out _)
                || slot.AllowedMode == TimeSlotAllowedMode.OnlineOnly)
            {
                result[key] = slot.AllowedMode;
            }
        }
        return result;
    }

    /// <summary>
    /// Loaded section entity'lerini paralel-split-aware RuntimeCandidate listesine çevirir.
    /// Classroom lookup: query'de Include edilen Classroom'lardan inşa edilir (primary kapasite hesabı için).
    /// </summary>
    private static List<RuntimeCandidate> ProjectRuntimeCandidates(
        List<CourseSection> sections,
        IReadOnlyDictionary<int, string> instructorNameMap,
        CourseSchedulingMetadata courseMetadata)
    {
        var classroomLookup = sections
            .Where(s => s.Classroom != null)
            .GroupBy(s => s.Classroom!.Id)
            .ToDictionary(g => g.Key, g => g.First().Classroom!);

        return sections
            .Select(section => BuildExistingRuntimeCandidate(
                section,
                instructorNameMap,
                courseMetadata.MinSuggestedSemesters,
                courseMetadata.SuggestedSemestersByDepartment,
                courseMetadata.AccessibleDepartments,
                courseMetadata.CompulsoryFlags,
                classroomLookup))
            .ToList();
    }

    /// <summary>
    /// Manuel atama (Apply) yolu da AutoScheduleEngine ile AYNI mode'u kullanır.
    /// ConflictPairsBuilder mode-branching tek otoritedir.
    /// </summary>
    private async Task<ConflictPairsResult> BuildConflictPairsAsync(
        IReadOnlyList<RuntimeCandidate> candidates,
        CourseSchedulingMetadata courseMetadata,
        string curriculumConflictMode,
        int enrollmentThreshold,
        int termId,
        CancellationToken ct)
    {
        // 2026-05-17 — CohortBuilder komşu yıl hard pair filtresi için Course → IsCompulsory.
        // Heuristic: bir ders birden çok müfredatta yer alıyorsa "en az birinde zorunlu" ise true.
        var courseIds = candidates.Select(c => c.CourseId).Distinct().ToList();
        var courseCompulsoryMap = courseIds.Count == 0
            ? new Dictionary<int, bool>()
            : await _context.CurriculumCourses
                .AsNoTracking()
                .Where(cc => courseIds.Contains(cc.CourseId))
                .GroupBy(cc => cc.CourseId)
                .Select(g => new { CourseId = g.Key, IsCompulsory = g.Any(cc => cc.IsCompulsory) })
                .ToDictionaryAsync(x => x.CourseId, x => x.IsCompulsory, ct);

        var cohortRefs = candidates
            .Select(c => new CohortSectionRef(
                c.SectionId, c.SectionNumber, c.CourseId,
                courseCompulsoryMap.GetValueOrDefault(c.CourseId, false)))
            .ToList();

        return await ConflictPairsBuilder.BuildAsync(
            curriculumConflictMode,
            cohortRefs,
            courseMetadata.SuggestedSemestersByDepartment,
            courseMetadata.AccessibleDepartments,
            enrollmentThreshold,
            _context, termId, ct);
    }

    private static RuntimeCandidate BuildExistingRuntimeCandidate(
        CourseSection section,
        IReadOnlyDictionary<int, string> instructorNameMap,
        IReadOnlyDictionary<int, int> semesterMap,
        IReadOnlyDictionary<int, Dictionary<int, int>> suggestedSemestersByDepartment,
        IReadOnlyDictionary<int, HashSet<int>> accessibleDepartments,
        IReadOnlyDictionary<int, bool> compulsoryFlags,
        IReadOnlyDictionary<int, Classroom>? classroomLookup)
    {
        var timeBlocks = ScheduleHelper.ToTimeBlocks(section.SectionSchedules);
        var instructorName = section.InstructorId.HasValue
            && instructorNameMap.TryGetValue(section.InstructorId.Value, out var n) ? n : string.Empty;
        var suggestedSemester = semesterMap.TryGetValue(section.CourseId, out var sem) ? sem : (int?)null;
        var isCompulsory = compulsoryFlags.TryGetValue(section.CourseId, out var comp) && comp;

        var candidate = ProjectRuntimeCandidate(
            section,
            section.Classroom,
            section.Instructor,
            instructorName,
            timeBlocks,
            suggestedSemester,
            GetSuggestedSemestersByDepartment(section.CourseId, suggestedSemestersByDepartment),
            GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, accessibleDepartments),
            classroomLookup);
        return candidate with { IsCompulsory = isCompulsory };
    }

    private async Task<(RuntimeCandidate Candidate, IReadOnlyList<string> Notes)> BuildCandidateRuntimeAsync(
        CourseSection section,
        int? instructorIdOverride,
        int? classroomIdOverride,
        List<ScheduleSlotDto>? scheduleSlotOverrides,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var instructorId = instructorIdOverride ?? section.InstructorId;
        var classroomId = classroomIdOverride ?? section.ClassroomId;

        Instructor? instructor = null;
        string instructorName = string.Empty;

        if (instructorId.HasValue)
        {
            instructor = await _context.Instructors
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == instructorId.Value, cancellationToken);

            if (instructor == null)
            {
                notes.Add("Seçilen öğretim üyesi bulunamadı.");
            }
            else
            {
                var weeklyHoursPolicy = await InstructorWeeklyHoursPolicy.LoadAsync(_systemConfig, cancellationToken);
                instructor.MaxWeeklyHours = weeklyHoursPolicy.Resolve(instructor.Title, instructor.MaxWeeklyHours);
                instructorName = await GetInstructorNameAsync(instructor, cancellationToken);
            }
        }
        else
        {
            notes.Add("Section için öğretim üyesi atanmamış.");
        }

        Classroom? classroom = null;
        if (classroomId.HasValue)
        {
            classroom = await _context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == classroomId.Value, cancellationToken);

            if (classroom == null)
            {
                notes.Add("Seçilen derslik bulunamadı.");
            }
        }
        else
        {
            notes.Add("Section için derslik atanmamış.");
        }

        var scheduleSlots = new List<TimeBlock>();

        if (scheduleSlotOverrides != null)
        {
            // Plan 11 sonrası: H1 ClassroomConflict per-block ClassroomId üzerinden eşleşir
            // (paralel-lab desteği). Manuel atama akışında dto.ClassroomId boş gelirse top-level
            // request.ClassroomId yansıtılır — aksi halde block.ClassroomId null kalır.
            var overrideValidation = ValidateTimeBlocks(scheduleSlotOverrides
                .Select(dto => new TimeBlock
                {
                    Day = (DayOfWeek)dto.Day,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    ClassroomId = dto.ClassroomId ?? classroomId
                })
                .ToList());

            scheduleSlots = overrideValidation.Slots;
        }
        else
        {
            scheduleSlots = await LoadSectionTimeBlocksAsync(section.Id, cancellationToken);
        }

        if (scheduleSlots.Count == 0)
        {
            notes.Add("Section için program slot tanımı bulunmuyor.");
        }

        var courseMetadata = await LoadCourseSchedulingMetadataAsync(section.TermId, [section.CourseId], cancellationToken);
        var suggestedSemester = courseMetadata.MinSuggestedSemesters.TryGetValue(section.CourseId, out var semester)
            ? semester
            : (int?)null;

        var candidate = BuildCandidateRuntime(
            section,
            instructor,
            instructorName,
            classroom,
            scheduleSlots,
            suggestedSemester,
            GetSuggestedSemestersByDepartment(section.CourseId, courseMetadata.SuggestedSemestersByDepartment),
            GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, courseMetadata.AccessibleDepartments));

        // 2026-05-20: H5 curriculum mode için kursun zorunlu/seçmeli durumunu set et.
        var isCompulsory = courseMetadata.CompulsoryFlags.TryGetValue(section.CourseId, out var comp) && comp;
        candidate = candidate with { IsCompulsory = isCompulsory };

        return (candidate, notes);
    }

    private static RuntimeCandidate BuildCandidateRuntime(
        CourseSection section,
        Instructor? instructor,
        string instructorName,
        Classroom? classroom,
        IReadOnlyList<TimeBlock> scheduleSlots,
        int? suggestedSemester = null,
        Dictionary<int, int>? suggestedSemestersByDepartment = null,
        HashSet<int>? accessibleDepartmentIds = null)
    {
        return ProjectRuntimeCandidate(
            section, classroom, instructor, instructorName,
            scheduleSlots, suggestedSemester,
            suggestedSemestersByDepartment ?? new Dictionary<int, int>(),
            accessibleDepartmentIds ?? new HashSet<int>(),
            classroomLookup: null);
    }

    /// <summary>
    /// CourseSection + classroom + instructor + time blocks → RuntimeCandidate.
    /// V1 <c>SectionSnapshotFactory.FromCourseSection</c> portu — paralel-split-aware primary
    /// classroom kapasite hesabı + RuntimeBlock listesi inşası.
    /// </summary>
    private static RuntimeCandidate ProjectRuntimeCandidate(
        CourseSection section,
        Classroom? classroom,
        Instructor? instructor,
        string instructorName,
        IReadOnlyList<TimeBlock> timeBlocks,
        int? suggestedSemester,
        Dictionary<int, int> suggestedSemestersByDept,
        HashSet<int> accessibleDepts,
        IReadOnlyDictionary<int, Classroom>? classroomLookup)
    {
        var (primaryId, primaryName, primaryCapacity, primaryType)
            = ResolvePrimaryClassroomMetadata(classroom, timeBlocks, classroomLookup);

        // BlockType atama — timeBlocks listesi sırasıyla (ScheduleWriter ile aynı politika):
        // ilk TheoryHours kadar slot Theory, gerisi Lab. Frontend slot listesini Theory önce, Lab
        // sonra gönderir; ScheduleWriter da bu sıraya güvenir. Bu sırayı koruyarak BlockType
        // atamak, HSO kuralının "Theory ve Lab aynı zaman aralığına yazıldığında çakışıyor"
        // durumunu doğru yakalamasını sağlar (aynı pencere farklı BlockType → fail).
        var theoryHours = section.Course.TheoryHours;
        var blocks = timeBlocks
            .Select((b, idx) => BuildRuntimeBlock(
                b, classroomLookup, primaryName, primaryType, primaryCapacity,
                idx < theoryHours ? ScheduleBlockType.Theory : ScheduleBlockType.Lab))
            .ToList();

        return new RuntimeCandidate
        {
            SectionId = section.Id,
            SectionNumber = section.SectionNumber,
            CourseId = section.CourseId,
            CourseCode = section.Course.Code,
            CourseName = section.Course.Name,
            DepartmentId = section.Course.DepartmentId,
            InstructorId = instructor?.Id,
            InstructorName = instructorName,
            InstructorMaxWeeklyHours = instructor?.MaxWeeklyHours ?? 0,
            Capacity = section.Capacity,
            EnrolledCount = section.EnrolledCount,
            TheoryHours = section.Course.TheoryHours,
            LabHours = section.Course.LabHours,
            TeachingMethod = section.Course.TeachingMethod,
            RequiredTheoryClassroomType = (int?)section.Course.TheoryClassroomType,
            RequiredLabClassroomType = (int?)section.Course.LabClassroomType,
            SuggestedSemester = suggestedSemester,
            SuggestedSemestersByDepartment = suggestedSemestersByDept,
            AccessibleDepartmentIds = accessibleDepts,
            PrimaryClassroomId = primaryId,
            PrimaryClassroomName = primaryName,
            PrimaryClassroomCapacity = primaryCapacity,
            PrimaryClassroomType = primaryType,
            Blocks = blocks,
        };
    }

    private static RuntimeBlock BuildRuntimeBlock(
        TimeBlock b,
        IReadOnlyDictionary<int, Classroom>? classroomLookup,
        string primaryName,
        ClassroomType primaryType,
        int primaryCapacity,
        ScheduleBlockType? blockType = null)
    {
        // Block kendi classroom'unu çözer (paralel-split: her block farklı oda olabilir).
        string blockName = primaryName;
        ClassroomType classroomType = primaryType;
        int blockCapacity = primaryCapacity;

        if (b.ClassroomId.HasValue && classroomLookup != null
            && classroomLookup.TryGetValue(b.ClassroomId.Value, out var cr))
        {
            blockName = cr.Name;
            classroomType = cr.Type;
            blockCapacity = cr.Capacity;
        }

        return new RuntimeBlock(
            Day: (int)b.Day,
            StartTime: b.StartTime,
            EndTime: b.EndTime,
            ClassroomId: b.ClassroomId,
            ClassroomName: blockName,
            ClassroomType: classroomType,
            ClassroomCapacity: blockCapacity,
            BlockType: blockType);
    }

    /// <summary>
    /// Primary window (ilk blok) classroom metadata'sı. Paralel-split: aynı (Day, Start, End)
    /// penceresinde birden çok oda → kapasite toplamı. Bloklar boş ise primaryClassroom fallback.
    /// V1 <c>SectionSnapshotFactory.ResolveClassroomMetadata</c> portu.
    /// </summary>
    private static (int? Id, string Name, int Capacity, ClassroomType Type) ResolvePrimaryClassroomMetadata(
        Classroom? primaryClassroom,
        IReadOnlyList<TimeBlock> blocks,
        IReadOnlyDictionary<int, Classroom>? classroomLookup)
    {
        if (blocks.Count == 0)
        {
            return (
                primaryClassroom?.Id,
                primaryClassroom?.Name ?? string.Empty,
                primaryClassroom?.Capacity ?? 0,
                primaryClassroom?.Type ?? ClassroomType.Lecture
            );
        }

        var firstBlock = blocks[0];
        var primaryId = firstBlock.ClassroomId ?? primaryClassroom?.Id;

        Classroom? resolvedPrimary = primaryClassroom;
        if (resolvedPrimary == null && primaryId.HasValue && classroomLookup != null)
            classroomLookup.TryGetValue(primaryId.Value, out resolvedPrimary);

        var name = resolvedPrimary?.Name ?? string.Empty;
        var type = resolvedPrimary?.Type ?? ClassroomType.Lecture;

        var primaryWindowBlocks = blocks
            .Where(b => b.Day == firstBlock.Day
                && b.StartTime == firstBlock.StartTime
                && b.EndTime == firstBlock.EndTime)
            .ToList();

        var combinedCapacity = 0;
        foreach (var b in primaryWindowBlocks)
        {
            if (b.ClassroomId.HasValue && classroomLookup != null
                && classroomLookup.TryGetValue(b.ClassroomId.Value, out var cr))
            {
                combinedCapacity += cr.Capacity;
            }
        }

        var capacity = combinedCapacity > 0
            ? combinedCapacity
            : (resolvedPrimary?.Capacity ?? 0);

        return (primaryId, name, capacity, type);
    }
}
