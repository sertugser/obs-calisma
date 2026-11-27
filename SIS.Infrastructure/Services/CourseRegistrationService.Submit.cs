using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// CourseRegistrationService — kayıt gönderme (SubmitRegistrationAsync).
/// Plan'daki dersleri Active'e geçirir; danışman onayına gönderir.
/// </summary>
public partial class CourseRegistrationService
{
    public async Task<RegistrationResponseDto> SubmitRegistrationAsync(int studentId, RegistrationSubmitDto dto)
    {
        var response = new RegistrationResponseDto();

        // Sınır doğrulaması: boş gönderiyi reddet (aksi halde sessizce "başarılı" döner, hiçbir şey yapmaz).
        if (dto.CourseSectionIds is null || dto.CourseSectionIds.Count == 0)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "EmptySelection", Message = "Kayit icin en az bir ders secmelisiniz." }
                }
            };
            return response;
        }

        // Validate the registration
        var validationResult = await _validationService.ValidateRegistrationAsync(studentId, dto.CourseSectionIds);

        if (!validationResult.IsValid)
        {
            response.Success = false;
            response.ValidationResult = validationResult;
            return response;
        }

        var submittedSections = await _context.CourseSections
            .Include(cs => cs.Course)
            .Where(cs => dto.CourseSectionIds.Contains(cs.Id))
            .ToListAsync();

        // Tüm şubeler aynı döneme ait olmalı (çapraz-dönem enjeksiyonu 500 yerine temiz hata).
        var distinctTermIds = submittedSections.Select(section => section.TermId).Distinct().ToList();
        if (distinctTermIds.Count > 1)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "MultipleTerms", Message = "Secilen subeler ayni akademik doneme ait olmalidir." }
                }
            };
            return response;
        }
        var activeTermId = distinctTermIds.Count == 1 ? (int?)distinctTermIds[0] : null;
        var webModuleGate = await ValidateStudentRegistrationWebModuleAsync(studentId, activeTermId);
        if (webModuleGate is not null)
        {
            response.Success = false;
            response.ValidationResult = webModuleGate;
            return response;
        }

        // Yayın kapısı (draft/publish) artık tek kaynaktan ValidateRegistrationAsync içinde
        // uygulanıyor (yukarıdaki validate çağrısı); burada ayrıca kontrol edilmez (ikibaşlılık yok).

        var advisorConstraintResult = await EvaluateAdvisorConstraintsAsync(
            studentId,
            submittedSections.Select(section => new AdvisorConstraintSectionInfo
            {
                CourseSectionId = section.Id,
                CourseId = section.CourseId,
                TermId = section.TermId,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name
            }));

        if (!advisorConstraintResult.IsValid)
        {
            response.Success = false;
            response.ValidationResult = advisorConstraintResult;
            return response;
        }

        validationResult.Warnings.AddRange(advisorConstraintResult.Warnings);

        // Check if student is scholarship (FR-52: skip payment confirmation)
        var student = await _context.Students.FindAsync(studentId);
        if (student == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "StudentExists", Message = "Ogrenci bulunamadi." }
                }
            };
            return response;
        }

        // Danışman onayı her zaman zorunlu — burslu öğrenci dahil (ADIM 11)
        var targetStatus = RegistrationStatus.PendingAdvisorApproval;

        // Load or create registrations for the submitted sections
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId
                && dto.CourseSectionIds.Contains(cr.CourseSectionId)
                && cr.Status == RegistrationStatus.Draft)
            .ToListAsync();

        // For sections that don't have a Draft registration yet, create them
        var existingCourseSectionIds = registrations.Select(r => r.CourseSectionId).ToHashSet();
        var missingSectionIds = dto.CourseSectionIds.Where(id => !existingCourseSectionIds.Contains(id)).ToList();

        if (missingSectionIds.Any())
        {
            var missingSections = await _context.CourseSections
                .Include(cs => cs.Course)
                .Where(cs => missingSectionIds.Contains(cs.Id))
                .ToListAsync();

            // Daha önce Dropped/Withdrawn olmuş ama unique constraint (Student+Section) nedeniyle
            // yeniden Add edilirse kırılacak kayıtları önceden yakala — bunları reactivate edeceğiz.
            var existingAny = await _context.CourseRegistrations
                .Where(cr => cr.StudentId == studentId && missingSectionIds.Contains(cr.CourseSectionId))
                .ToListAsync();
            var existingByDropped = existingAny
                .Where(cr => cr.Status == RegistrationStatus.Dropped
                    || cr.Status == RegistrationStatus.Withdrawn
                    || cr.Status == RegistrationStatus.Rejected)
                .ToDictionary(cr => cr.CourseSectionId);

            // Onay bekleyen ya da kesinleşmiş kayıt yeniden eklenemez (tekil dizin); bir şube bile böyleyse hiçbiri gönderilmez.
            var alreadySubmitted = AlreadySubmittedResult(existingAny, missingSections);
            if (alreadySubmitted is not null)
            {
                response.Success = false;
                response.ValidationResult = alreadySubmitted;
                return response;
            }

            // === KRİTİK BÖLGE: Serializable transaction + ders başına fresh kapasite kontrolü + increment ===
            // Çift sayımı önler (B2), kapasite kontrolünü tüm kayıt noktalarında zorunlu hale getirir (B3).
            // Eşzamanlılık çakışması "kontenjan doldu" demek değildir: deneme geri alınır ve taze işlemle yinelenir (D23).
            var useTransaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            var statusesBeforeAttempts = existingAny.ToDictionary(cr => cr.Id, cr => cr.Status);
            // Geri sarma yalnız denemelerden önce izleyicide bekleyen değişiklik yokken güvenlidir.
            var canRewind = useTransaction && !_context.ChangeTracker.HasChanges();
            for (var attempt = 1; ; attempt++)
            {
                using var attemptChanges = new SubmitAttemptChanges(_context.ChangeTracker);
                var commitAttempted = false;
                await using var tx = useTransaction
                    ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                    : null;
                try
                {
                    foreach (var section in missingSections)
                    {
                        // Tek kaynak: SeatLedger taze kapasite kontrolü + (cross-list grubu dahil) rezervasyon.
                        if (!await SeatLedger.TryReserveAsync(_context, section))
                        {
                            if (tx != null) await tx.RollbackAsync();
                            var effective = await SeatLedger.GetEffectiveEnrolledAsync(_context, section);
                            response.Success = false;
                            response.ValidationResult = new ValidationResultDto
                            {
                                IsValid = false,
                                Errors = new List<ValidationError>
                                {
                                    new() { Rule = "Capacity", Message = $"'{section.Course.Code} {section.Course.Name}' dersinin kontenjanı bu esnada doldu ({effective}/{section.Capacity}).", CourseSectionId = section.Id }
                                }
                            };
                            return response;
                        }

                        Domain.Entities.CourseRegistration reg;
                        if (existingByDropped.TryGetValue(section.Id, out var droppedReg))
                        {
                            // Reactivate — yeni kayıt oluşturmak unique constraint'i kırardı
                            droppedReg.Status = RegistrationStatus.Draft;
                            droppedReg.UpdatedAt = DateTime.UtcNow;
                            reg = droppedReg;
                        }
                        else
                        {
                            reg = new Domain.Entities.CourseRegistration
                            {
                                StudentId = studentId,
                                CourseSectionId = section.Id,
                                Status = RegistrationStatus.Draft,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.CourseRegistrations.Add(reg);
                        }

                        // Madde 6 tamamlama: AKTS + kredi snapshot + havuz ataması + (Faz 4) Açma Nedeni id snapshot
                        var (snapshotEcts, snapshotCredit, appliedPoolId, openingReasonIdSnapshot) = await ResolveCourseSnapshotsAsync(studentId, section.CourseId);
                        reg.EctsSnapshot = snapshotEcts;
                        reg.CreditSnapshot = snapshotCredit;
                        reg.AppliedElectivePoolId = appliedPoolId;
                        reg.OpeningReasonIdSnapshot = openingReasonIdSnapshot;

                        // Faz 4 — BypassMandatoryAttendance flag'i: bu ders için devam zorunluluğunu false zorla.
                        if (openingReasonIdSnapshot.HasValue)
                        {
                            var bypassAttendance = await _context.CurriculumCourseOpeningReasons
                                .AsNoTracking()
                                .AnyAsync(r => r.Id == openingReasonIdSnapshot.Value && r.BypassMandatoryAttendance);
                            if (bypassAttendance)
                                reg.MandatoryAttendance = false;
                        }

                        // Koltuk rezervasyonu SeatLedger.TryReserveAsync içinde yapıldı.
                        registrations.Add(reg);
                    }
                    await _context.SaveChangesAsync();
                    commitAttempted = true;
                    if (tx != null) await tx.CommitAsync();
                    break;
                }
                catch (DbUpdateException ex) when (IsAlreadySubmittedConflict(ex))
                {
                    // Ön denetimden sonra aynı öğrenci-şube kaydı başka bir işlemde kesinleşti: ön denetimle aynı ret.
                    if (tx != null) await tx.RollbackAsync();
                    var currentRegistrations = await _context.CourseRegistrations.AsNoTracking()
                        .Where(cr => cr.StudentId == studentId && missingSectionIds.Contains(cr.CourseSectionId))
                        .ToListAsync();
                    var raceResult = AlreadySubmittedResult(currentRegistrations, missingSections);
                    if (raceResult is null) throw;
                    response.Success = false;
                    response.ValidationResult = raceResult;
                    return response;
                }
                catch (Exception ex) when (SeatLedger.IsConcurrencyConflict(ex))
                {
                    // Eş zamanlı kayıt çakışması (RowVersion / Serializable): taze işlemle yeniden dene; denemeler biterse
                    // ya da geri sarma güvenli değilse 500 yerine kullanıcı mesajı. Onay (commit) düştüyse işlem zaten bitmiştir:
                    // geri alma çağrısı hata verir; işlem yalnız bırakılır.
                    if (tx != null)
                    {
                        if (commitAttempted) await tx.DisposeAsync();
                        else await tx.RollbackAsync();
                    }
                    if (canRewind && attempt < SubmitConflictMaxAttempts
                        && await TryRewindSubmitAttemptAsync(studentId, missingSectionIds, statusesBeforeAttempts, attemptChanges))
                        continue;
                    response.Success = false;
                    response.ValidationResult = new ValidationResultDto
                    {
                        IsValid = false,
                        Errors = new List<ValidationError>
                        {
                            new() { Rule = "Capacity", Message = "Seçtiğiniz şubelerden birinin kontenjanı bu esnada değişti. Lütfen tekrar deneyin." }
                        }
                    };
                    return response;
                }
                catch
                {
                    if (tx != null) await tx.RollbackAsync();
                    throw;
                }
            }

            // Reload to get navigation properties
            registrations = await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                    .ThenInclude(cs => cs.Course)
                .Where(cr => cr.StudentId == studentId
                    && dto.CourseSectionIds.Contains(cr.CourseSectionId)
                    && cr.Status == RegistrationStatus.Draft)
                .ToListAsync();
        }

        // Update all Draft -> target status
        foreach (var reg in registrations)
        {
            reg.Status = targetStatus;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        // Update RegistrationPlan status to Submitted
        var termId = registrations.FirstOrDefault()?.CourseSection.TermId;
        if (termId.HasValue)
        {
            var plan = await _context.RegistrationPlans
                .FirstOrDefaultAsync(rp => rp.StudentId == studentId && rp.TermId == termId.Value);
            if (plan != null)
            {
                plan.Status = RegistrationPlanStatus.Submitted;
                plan.SubmittedAt = DateTime.UtcNow;
                plan.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        // Bildirim: öğrencinin danışmanına "kayıt onay bekliyor". Null-safe + opsiyonel; push hatası akışı bozmaz.
        if (_notifications is not null && registrations.Count > 0 && student.AdvisorId is int submitAdvisorId)
        {
            var advisorUserId = await _context.Instructors
                .Where(i => i.Id == submitAdvisorId)
                .Select(i => (int?)i.UserId)
                .FirstOrDefaultAsync();
            if (advisorUserId is int advisorUid)
            {
                await _notifications.NotifyAsync(
                    advisorUid,
                    NotificationType.RegistrationSubmittedForApproval,
                    "Ders kaydı onay bekliyor",
                    $"{student.StudentNumber} numaralı öğrenci {registrations.Count} dersini onayınıza gönderdi.",
                    link: "/ders-kayit-onay",
                    severity: NotificationSeverity.Info,
                    entityRef: $"student:{student.Id}");
            }
        }

        response.Success = true;
        response.ValidationResult = validationResult; // May contain NeedsApproval warnings
        response.Registrations = registrations.Select(r => new RegistrationItemDto
        {
            Id = r.Id,
            CourseSectionId = r.CourseSectionId,
            CourseCode = r.CourseSection.Course.Code,
            CourseName = r.CourseSection.Course.Name,
            Ects = r.CourseSection.Course.Ects,
            Status = r.Status.ToString()
        }).ToList();

        return response;
    }

    /// <summary>Eşzamanlılık çakışmasında gönderim kritik bölgesinin toplam deneme sayısı (teknik sınır, kurumsal kural değil).</summary>
    private const int SubmitConflictMaxAttempts = 3;

    /// <summary>
    /// Çakışan gönderim denemesinin izleyicideki izlerini, türü ne olursa olsun geri sarar: denemede eklenen her varlık (denetim
    /// kesicisinin eklediği denetim satırları dahil) izlenmez olur, değişen her varlık veritabanındaki hâline döner. Öğrencinin bu
    /// şubelerdeki kayıtları denemelerden önceki hâlinden farklıysa (ör. aynı öğrencinin başka bir isteği araya girdiyse) ya da
    /// değişen bir varlığın satırı artık yoksa false döner: yeniden deneme koltuğu ikinci kez sayabilir ya da denetimsiz sürebilirdi.
    /// </summary>
    private async Task<bool> TryRewindSubmitAttemptAsync(
        int studentId,
        List<int> sectionIds,
        Dictionary<int, RegistrationStatus> statusesBefore,
        SubmitAttemptChanges changes)
    {
        var current = await _context.CourseRegistrations.AsNoTracking()
            .Where(cr => cr.StudentId == studentId && sectionIds.Contains(cr.CourseSectionId))
            .Select(cr => new { cr.Id, cr.Status })
            .ToListAsync();
        if (current.Count != statusesBefore.Count
            || current.Any(cr => !statusesBefore.TryGetValue(cr.Id, out var before) || before != cr.Status))
            return false;

        foreach (var entity in changes.Added.ToList())
            _context.Entry(entity).State = EntityState.Detached;
        foreach (var entry in changes.Modified.Where(e => !changes.Added.Contains(e)).Select(e => _context.Entry(e)).ToList())
        {
            if (entry.State != EntityState.Detached) await entry.ReloadAsync();
            if (entry.State == EntityState.Detached) return false;
        }
        return true;
    }

    /// <summary>Bir gönderim denemesinde izleyiciye eklenen ve izleyicide değişen varlıklar; dispose edilince kayıt durur.</summary>
    private sealed class SubmitAttemptChanges : IDisposable
    {
        private readonly ChangeTracker _tracker;
        public HashSet<object> Added { get; } = new(ReferenceEqualityComparer.Instance);
        public HashSet<object> Modified { get; } = new(ReferenceEqualityComparer.Instance);

        public SubmitAttemptChanges(ChangeTracker tracker)
        {
            _tracker = tracker;
            _tracker.Tracked += OnTracked;
            _tracker.StateChanged += OnStateChanged;
        }

        private void OnTracked(object? sender, EntityTrackedEventArgs e) { if (!e.FromQuery) Record(e.Entry.Entity, e.Entry.State); }
        private void OnStateChanged(object? sender, EntityStateChangedEventArgs e) => Record(e.Entry.Entity, e.NewState);

        private void Record(object entity, EntityState state)
        {
            if (state == EntityState.Added) Added.Add(entity);
            else if (state is EntityState.Modified or EntityState.Deleted) Modified.Add(entity);
        }

        public void Dispose()
        {
            _tracker.Tracked -= OnTracked;
            _tracker.StateChanged -= OnStateChanged;
        }
    }

    /// <summary>
    /// Öğrencinin onay bekleyen, etkin ya da tamamlanmış kaydı olan her talep edilen şube için bir AlreadySubmitted hatası;
    /// böyle şube yoksa null.
    /// </summary>
    private static ValidationResultDto? AlreadySubmittedResult(
        IEnumerable<Domain.Entities.CourseRegistration> existing, IEnumerable<Domain.Entities.CourseSection> sections)
    {
        var submittedSectionIds = existing
            .Where(cr => cr.Status is RegistrationStatus.PendingAdvisorApproval
                or RegistrationStatus.Active
                or RegistrationStatus.Completed)
            .Select(cr => cr.CourseSectionId)
            .ToHashSet();
        var errors = sections
            .Where(section => submittedSectionIds.Contains(section.Id))
            .Select(section => new ValidationError
            {
                Rule = "AlreadySubmitted",
                Message = $"'{section.Course.Code} {section.Course.Name}' dersine kaydınız zaten gönderildi (onay bekliyor veya kesinleşti).",
                CourseSectionId = section.Id
            })
            .ToList();
        return errors.Count == 0 ? null : new ValidationResultDto { IsValid = false, Errors = errors };
    }

    /// <summary>Yalnız öğrenci-şube tekil dizininin ihlali (23505); başka kısıt ya da hata eşleşmez ve yayılır.</summary>
    internal static bool IsAlreadySubmittedConflict(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException
        {
            SqlState: "23505",
            ConstraintName: "IX_CourseRegistrations_StudentId_CourseSectionId"
        };
}
