using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Data;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace RoghtiaSystem_JahidKawa.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/treatments")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class TreatmentsController(MainDbContext context) : ControllerBase
    {
        private int LoggedInUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        private IQueryable<Treatment> OwnedTreatments => context.Treatments.Where(t => t.UserId == LoggedInUserId);
        private static readonly TimeSpan KabulOffset = TimeSpan.FromMinutes(270);
        private static readonly HashSet<string> AllowedFrequencies = new(StringComparer.Ordinal)
        {
            "Once daily", "Twice daily", "Three times daily", "Four times daily",
            "Every 4 hours", "Every 6 hours", "Every 8 hours", "Every 12 hours", "Every 24 hours",
            "As needed", "Immediately", "Single dose", "At bedtime", "Every morning", "Every evening",
            "Before meals", "After meals", "Before breakfast", "After breakfast", "Before lunch", "After lunch",
            "Before dinner", "After dinner", "With meals", "Every other day", "Weekly", "Twice weekly", "Monthly"
        };

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] TreatmentQuery query, CancellationToken cancellationToken)
        {
            if (query.FromUtc.HasValue != query.ToUtc.HasValue ||
                (query.FromUtc.HasValue && (query.FromUtc.Value.Kind != DateTimeKind.Utc || query.ToUtc!.Value.Kind != DateTimeKind.Utc ||
                    query.FromUtc >= query.ToUtc || query.ToUtc - query.FromUtc > TimeSpan.FromDays(32))))
                return BadRequest(new AuthError("VALIDATION_ERROR", "Invalid date selection."));

            var now = DateTime.UtcNow;
            var todayInKabul = (now + KabulOffset).Date;
            var from = query.FromUtc ?? todayInKabul - KabulOffset;
            var to = query.ToUtc ?? from.AddDays(1);
            var treatments = OwnedTreatments.AsNoTracking().Where(t => t.RecordedAtUtc >= from && t.RecordedAtUtc < to);
            if (!string.IsNullOrWhiteSpace(query.PatientName))
            {
                var search = query.PatientName.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                treatments = treatments.Where(t => EF.Functions.Like(t.Patient.Name, $"%{search}%", "\\"));
            }
            if (query.HasPrescription) treatments = treatments.Where(t => t.PrescriptionItems.Any());
            var total = await treatments.CountAsync(cancellationToken);
            var offset = ((long)query.Page - 1) * query.PageSize;
            var items = offset >= total ? [] : await treatments.OrderByDescending(t => t.RecordedAtUtc).ThenByDescending(t => t.Id)
                .Skip((int)offset).Take(query.PageSize)
                .Select(t => new TreatmentSummaryResponse(t.Id, t.RecordedAtUtc,
                    new TreatmentPatientResponse(t.Patient.Id, t.Patient.Code, t.Patient.Name, t.Patient.Phone,
                        t.Patient.Age, t.Patient.Gender, t.Patient.Address), t.BloodPressure,
                    t.PrescriptionItems.Count, t.ClinicalNotes.Count)).ToListAsync(cancellationToken);
            return Ok(new TreatmentListResponse(items, total, query.Page, query.PageSize));
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] DateOnly? date, [FromQuery] string? period, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var todayInKabul = (now + KabulOffset).Date;
            var todayFromUtc = todayInKabul - KabulOffset;
            var tomorrowFromUtc = todayFromUtc.AddDays(1);
            var owned = OwnedTreatments.AsNoTracking();
            var totalPatients = await context.Patients.CountAsync(p => p.UserId == LoggedInUserId, cancellationToken);
            var todayPatients = await owned.Where(t => t.RecordedAtUtc >= todayFromUtc && t.RecordedAtUtc < tomorrowFromUtc)
                .Select(t => t.PatientId).Distinct().CountAsync(cancellationToken);
            var todayTreatments = await owned.CountAsync(t => t.RecordedAtUtc >= todayFromUtc && t.RecordedAtUtc < tomorrowFromUtc, cancellationToken);
            var prescriptionsToday = await owned.CountAsync(t => t.RecordedAtUtc >= todayFromUtc && t.RecordedAtUtc < tomorrowFromUtc
                && t.PrescriptionItems.Any(), cancellationToken);
            var medicationCount = await context.Medications.CountAsync(m => m.UserId == LoggedInUserId, cancellationToken);
            var recent = await owned.OrderByDescending(t => t.RecordedAtUtc).ThenByDescending(t => t.Id).Take(8)
                .Select(t => new TreatmentSummaryResponse(t.Id, t.RecordedAtUtc,
                    new TreatmentPatientResponse(t.Patient.Id, t.Patient.Code, t.Patient.Name, t.Patient.Phone,
                        t.Patient.Age, t.Patient.Gender, t.Patient.Address), t.BloodPressure,
                    t.PrescriptionItems.Count, t.ClinicalNotes.Count)).ToListAsync(cancellationToken);

            var selectedDate = date ?? DateOnly.FromDateTime(todayInKabul);
            var selectedDay = selectedDate.ToDateTime(TimeOnly.MinValue);
            var selectedPeriod = period is "Day" or "Week" or "Month" ? period : "Week";
            var daysBefore = selectedPeriod switch
            {
                "Day" => 0,
                "Month" => selectedDay.Day - 1,
                _ => ((int)selectedDay.DayOfWeek + 1) % 7
            };
            var chartStart = selectedDay.AddDays(-daysBefore);
            var chartEnd = selectedPeriod == "Month" ? chartStart.AddMonths(1) : chartStart.AddDays(selectedPeriod == "Day" ? 1 : 7);
            var chartFromUtc = chartStart - KabulOffset;
            var chartToUtc = chartEnd - KabulOffset;
            var chartRows = await owned.Where(t => t.RecordedAtUtc >= chartFromUtc && t.RecordedAtUtc < chartToUtc)
                .GroupBy(t => t.RecordedAtUtc.AddMinutes(270).Date)
                .Select(group => new { Date = group.Key, Count = group.Count(), WithPrescription = group.Count(t => t.PrescriptionItems.Any()) })
                .ToListAsync(cancellationToken);
            var bucketCount = selectedPeriod switch { "Day" => 1, "Month" => DateTime.DaysInMonth(chartStart.Year, chartStart.Month), _ => 7 };
            var visits = Enumerable.Range(0, bucketCount).Select(offset =>
            {
                var bucketDate = chartStart.AddDays(offset);
                var bucket = chartRows.SingleOrDefault(row => row.Date == bucketDate);
                return new DashboardVisitDay(bucketDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    bucket?.Count ?? 0, bucket?.WithPrescription ?? 0);
            }).ToList();
            return Ok(new DashboardSummaryResponse(totalPatients, todayPatients, todayTreatments,
                prescriptionsToday, medicationCount, visits, recent));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var treatment = await OwnedTreatments.AsNoTracking().AsSplitQuery()
                .Include(t => t.Patient).Include(t => t.PrescriptionItems).Include(t => t.ClinicalNotes).ThenInclude(l => l.ClinicalNote)
                .Include(t => t.AdditionalInstructionItems).ThenInclude(link => link.AdditionalInstruction)
                .Include(t => t.DiagnosticInvestigations).ThenInclude(link => link.DiagnosticInvestigation)
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
            return treatment == null ? NotFound(new AuthError("NOT_FOUND", "Treatment not found.")) : Ok(ToResponse(treatment));
        }

        [HttpGet("templates")]
        public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
        {
            var templates = await context.TreatmentTemplates.AsNoTracking().Where(item => item.UserId == LoggedInUserId)
                .OrderBy(item => item.Name).ToListAsync(cancellationToken);
            return Ok(templates.Select(ToTemplateResponse).ToList());
        }

        [HttpPost]
        [Consumes("application/json")]
        [RequestSizeLimit(32 * 1024)]
        public async Task<IActionResult> Create([FromBody] TreatmentRequest request, CancellationToken cancellationToken)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var (error, medications) = await ValidateReferences(request, cancellationToken);
            if (error != null) return error;
            var instructionError = await ValidateInstructionReferences(request, cancellationToken);
            if (instructionError != null) return instructionError;
            var diagnosticError = await ValidateDiagnosticInvestigationReferences(request, cancellationToken);
            if (diagnosticError != null) return diagnosticError;
            var (stockError, deductedMedicationIds) = await ApplyStockChanges(request, [], cancellationToken);
            if (stockError != null) return stockError;
            var treatment = new Treatment { UserId = LoggedInUserId, PatientId = request.PatientId!.Value,
                RecordedAtUtc = DateTime.UtcNow, BloodPressure = request.BloodPressure?.Trim() ?? "120/80",
                AdditionalInstructions = request.AdditionalInstructions?.Trim() ?? string.Empty };
            ApplyChanges(treatment, request, medications, deductedMedicationIds);
            context.Treatments.Add(treatment);
            await context.SaveChangesAsync(cancellationToken);
            var templateError = await SaveTemplateIfRequested(request, cancellationToken);
            if (templateError != null) return templateError;
            await transaction.CommitAsync(cancellationToken);
            var saved = await LoadOwned(treatment.Id, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = saved.Id }, ToResponse(saved));
        }

        [HttpPut("{id:int}")]
        [Consumes("application/json")]
        [RequestSizeLimit(32 * 1024)]
        public async Task<IActionResult> Update(int id, [FromBody] TreatmentRequest request, CancellationToken cancellationToken)
        {
            var treatment = await OwnedTreatments.Include(t => t.PrescriptionItems).Include(t => t.ClinicalNotes)
                .Include(t => t.AdditionalInstructionItems)
                .Include(t => t.DiagnosticInvestigations)
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (treatment == null) return NotFound(new AuthError("NOT_FOUND", "Treatment not found."));
            var (error, medications) = await ValidateReferences(request, cancellationToken);
            if (error != null) return error;
            var instructionError = await ValidateInstructionReferences(request, cancellationToken);
            if (instructionError != null) return instructionError;
            var diagnosticError = await ValidateDiagnosticInvestigationReferences(request, cancellationToken);
            if (diagnosticError != null) return diagnosticError;

            // Removing old rows before inserting their replacements avoids the unique position index.
            // Both saves are in one transaction, so an invalid replacement never erases a prescription.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var oldItems = treatment.PrescriptionItems.ToList();
            var (stockError, deductedMedicationIds) = await ApplyStockChanges(request, oldItems, cancellationToken);
            if (stockError != null) return stockError;
            context.PrescriptionItems.RemoveRange(treatment.PrescriptionItems);
            context.TreatmentClinicalNotes.RemoveRange(treatment.ClinicalNotes);
            context.TreatmentAdditionalInstructions.RemoveRange(treatment.AdditionalInstructionItems);
            context.TreatmentDiagnosticInvestigations.RemoveRange(treatment.DiagnosticInvestigations);
            await context.SaveChangesAsync(cancellationToken);
            treatment.PrescriptionItems.Clear();
            treatment.ClinicalNotes.Clear();
            treatment.AdditionalInstructionItems.Clear();
            treatment.DiagnosticInvestigations.Clear();
            treatment.PatientId = request.PatientId!.Value;
            treatment.BloodPressure = request.BloodPressure?.Trim() ?? "120/80";
            treatment.AdditionalInstructions = request.AdditionalInstructions?.Trim() ?? string.Empty;
            ApplyChanges(treatment, request, medications, deductedMedicationIds);
            await context.SaveChangesAsync(cancellationToken);
            var templateError = await SaveTemplateIfRequested(request, cancellationToken);
            if (templateError != null) return templateError;
            await transaction.CommitAsync(cancellationToken);
            return Ok(ToResponse(await LoadOwned(id, cancellationToken)));
        }

        private async Task<IActionResult?> SaveTemplateIfRequested(TreatmentRequest request, CancellationToken cancellationToken)
        {
            if (!request.SaveAsTemplate) return null;

            var ownerId = LoggedInUserId;
            TreatmentTemplate? template = null;
            if (request.TemplateId.HasValue)
            {
                template = await context.TreatmentTemplates.SingleOrDefaultAsync(item =>
                    item.Id == request.TemplateId.Value && item.UserId == ownerId, cancellationToken);
                if (template == null) return NotFound(new AuthError("NOT_FOUND", "Treatment template not found."));
            }

            var name = request.TemplateName?.Trim() ?? string.Empty;
            if (template == null && (name.Length is < 1 or > 100))
                return BadRequest(new AuthError("VALIDATION_ERROR", "Enter a name for the new treatment template."));
            if (template != null) name = template.Name;

            var exceptId = template?.Id ?? 0;
            var normalizedName = name.ToLowerInvariant();
            var duplicate = await context.TreatmentTemplates.AsNoTracking().AnyAsync(item => item.UserId == ownerId &&
                item.Name.ToLower() == normalizedName && item.Id != exceptId, cancellationToken);
            if (duplicate) return Conflict(new AuthError("TEMPLATE_NAME_EXISTS", "A treatment template with this name already exists."));

            var data = new TreatmentTemplateData
            {
                BloodPressure = request.BloodPressure?.Trim() ?? "120/80",
                AdditionalInstructions = request.AdditionalInstructions?.Trim() ?? string.Empty,
                ClinicalNoteIds = request.ClinicalNoteIds!,
                AdditionalInstructionIds = request.AdditionalInstructionIds!,
                DiagnosticIds = request.DiagnosticIds ?? [],
                InvestigationIds = request.InvestigationIds ?? [],
                DiagnosticInvestigationIds = GetDiagnosticInvestigationIds(request),
                PrescriptionItems = request.PrescriptionItems!.Select(item => new TreatmentTemplateItem(
                    item.MedicationId!.Value, item.Dose!.Value, item.Frequency!, item.Duration!.Trim())).ToList()
            };
            var dataJson = JsonSerializer.Serialize(data);
            if (template == null)
            {
                context.TreatmentTemplates.Add(new TreatmentTemplate { UserId = ownerId, Name = name, DataJson = dataJson });
            }
            else
            {
                template.DataJson = dataJson;
            }
            await context.SaveChangesAsync(cancellationToken);
            return null;
        }

        private static TreatmentTemplateResponse ToTemplateResponse(TreatmentTemplate template)
        {
            var data = JsonSerializer.Deserialize<TreatmentTemplateData>(template.DataJson) ?? new TreatmentTemplateData();
            var hasSplitData = data.DiagnosticIds.Count > 0 || data.InvestigationIds.Count > 0;
            var diagnostics = hasSplitData ? data.DiagnosticIds : data.DiagnosticInvestigationIds;
            var allDiagnosticInvestigations = hasSplitData
                ? data.DiagnosticIds.Concat(data.InvestigationIds).ToList()
                : data.DiagnosticInvestigationIds;
            return new TreatmentTemplateResponse(template.Id, template.Name, data.BloodPressure, data.AdditionalInstructions,
                data.ClinicalNoteIds, data.AdditionalInstructionIds, diagnostics, data.InvestigationIds,
                allDiagnosticInvestigations, data.PrescriptionItems);
        }

        private async Task<(IActionResult? Error, Dictionary<int, Medication> Medications)> ValidateReferences(
            TreatmentRequest request, CancellationToken cancellationToken)
        {
            var ownerId = LoggedInUserId;
            if (!await context.Patients.AsNoTracking().AnyAsync(p => p.Id == request.PatientId && p.UserId == ownerId, cancellationToken))
                return (BadRequest(new AuthError("VALIDATION_ERROR", "Select one of your patients.")), []);
            var noteIds = request.ClinicalNoteIds!;
            if (noteIds.Any(id => id < 1) || noteIds.Distinct().Count() != noteIds.Count ||
                await context.ClinicalNotes.CountAsync(n => n.UserId == ownerId && noteIds.Contains(n.Id), cancellationToken) != noteIds.Count)
                return (BadRequest(new AuthError("VALIDATION_ERROR", "Invalid clinical notes.")), []);
            var items = request.PrescriptionItems!;
            if (items.Any(item => string.IsNullOrWhiteSpace(item.Duration) || !AllowedFrequencies.Contains(item.Frequency ?? string.Empty)))
                return (BadRequest(new AuthError("VALIDATION_ERROR", "Select a valid frequency and enter the medication duration.")), []);
            var medicationIds = items.Select(item => item.MedicationId!.Value).Distinct().ToList();
            var medications = await context.Medications.AsNoTracking().Where(m => m.UserId == ownerId && medicationIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, cancellationToken);
            if (medications.Count != medicationIds.Count)
                return (BadRequest(new AuthError("VALIDATION_ERROR", "Select your registered medications.")), []);
            return (null, medications);
        }

        private async Task<IActionResult?> ValidateInstructionReferences(TreatmentRequest request, CancellationToken cancellationToken)
        {
            var ids = request.AdditionalInstructionIds!;
            if (ids.Any(id => id < 1) || ids.Distinct().Count() != ids.Count)
                return BadRequest(new AuthError("VALIDATION_ERROR", "Invalid additional instructions."));
            var count = await context.AdditionalInstructions.AsNoTracking()
                .CountAsync(item => item.UserId == LoggedInUserId && ids.Contains(item.Id), cancellationToken);
            return count == ids.Count ? null : BadRequest(new AuthError("VALIDATION_ERROR", "Invalid additional instructions."));
        }

        private async Task<IActionResult?> ValidateDiagnosticInvestigationReferences(TreatmentRequest request, CancellationToken cancellationToken)
        {
            var diagnosticIds = GetDiagnosticIds(request);
            var investigationIds = request.InvestigationIds ?? [];
            var allIds = diagnosticIds.Concat(investigationIds).ToList();
            if (allIds.Count > 20 || allIds.Any(id => id < 1) || allIds.Distinct().Count() != allIds.Count)
                return BadRequest(new AuthError("VALIDATION_ERROR", "Invalid diagnostics and investigations."));
            var diagnostics = await context.DiagnosticInvestigations.AsNoTracking()
                .CountAsync(item => item.UserId == LoggedInUserId && item.Kind == "Diagnostic" && diagnosticIds.Contains(item.Id), cancellationToken);
            var investigations = await context.DiagnosticInvestigations.AsNoTracking()
                .CountAsync(item => item.UserId == LoggedInUserId && item.Kind == "Investigation" && investigationIds.Contains(item.Id), cancellationToken);
            return diagnostics == diagnosticIds.Count && investigations == investigationIds.Count
                ? null : BadRequest(new AuthError("VALIDATION_ERROR", "Invalid diagnostics and investigations."));
        }

        private static List<int> GetDiagnosticIds(TreatmentRequest request)
        {
            var diagnosticIds = request.DiagnosticIds ?? [];
            var investigationIds = request.InvestigationIds ?? [];
            if (diagnosticIds.Count == 0 && investigationIds.Count == 0 && request.DiagnosticInvestigationIds is not null)
                return request.DiagnosticInvestigationIds;
            return diagnosticIds;
        }

        private static List<int> GetDiagnosticInvestigationIds(TreatmentRequest request)
        {
            var diagnosticIds = request.DiagnosticIds ?? [];
            var investigationIds = request.InvestigationIds ?? [];
            if (diagnosticIds.Count == 0 && investigationIds.Count == 0 && request.DiagnosticInvestigationIds is not null)
                return request.DiagnosticInvestigationIds;
            return diagnosticIds.Concat(investigationIds).Distinct().ToList();
        }

        private async Task<(IActionResult? Error, HashSet<int> DeductedMedicationIds)> ApplyStockChanges(
            TreatmentRequest request, IReadOnlyCollection<PrescriptionItem> oldItems, CancellationToken cancellationToken)
        {
            var ownerId = LoggedInUserId;
            foreach (var restored in oldItems.Where(item => item.StockDeducted).GroupBy(item => item.MedicationId)
                .Select(group => new { MedicationId = group.Key, Dose = group.Sum(item => (long)item.Dose) }))
            {
                if (restored.Dose > int.MaxValue)
                    return (BadRequest(new AuthError("VALIDATION_ERROR", "Medication dose is too large.")), []);
                await context.Medications.Where(m => m.Id == restored.MedicationId && m.UserId == ownerId)
                    .ExecuteUpdateAsync(update => update.SetProperty(m => m.Quantity, m => m.Quantity + (int)restored.Dose), cancellationToken);
            }

            var deducted = new HashSet<int>();
            foreach (var requested in request.PrescriptionItems!.GroupBy(item => item.MedicationId!.Value)
                .Select(group => new { MedicationId = group.Key, Dose = group.Sum(item => (long)item.Dose!.Value) }))
            {
                if (requested.Dose > int.MaxValue)
                    return (BadRequest(new AuthError("VALIDATION_ERROR", "Medication dose is too large.")), []);
                var dose = (int)requested.Dose;
                var changed = await context.Medications
                    .Where(m => m.Id == requested.MedicationId && m.UserId == ownerId && m.Quantity >= dose)
                    .ExecuteUpdateAsync(update => update.SetProperty(m => m.Quantity, m => m.Quantity - dose), cancellationToken);
                if (changed == 1) deducted.Add(requested.MedicationId);
                else if (!request.AllowInsufficientStock)
                    return (Conflict(new AuthError("INSUFFICIENT_STOCK", "The selected medication has insufficient stock for this dose.")), []);
            }
            return (null, deducted);
        }

        private static void ApplyChanges(Treatment treatment, TreatmentRequest request, Dictionary<int, Medication> medications,
            HashSet<int> deductedMedicationIds)
        {
            var items = request.PrescriptionItems!;
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var medication = medications[item.MedicationId!.Value];
                treatment.PrescriptionItems.Add(new PrescriptionItem { Position = index + 1, MedicationId = medication.Id,
                    MedicationName = medication.Name, MedicationType = medication.Type, Dose = item.Dose!.Value,
                    StockDeducted = deductedMedicationIds.Contains(medication.Id), Frequency = item.Frequency!, Duration = item.Duration!.Trim() });
            }
            foreach (var noteId in request.ClinicalNoteIds!) treatment.ClinicalNotes.Add(new TreatmentClinicalNote { ClinicalNoteId = noteId });
            foreach (var instructionId in request.AdditionalInstructionIds!)
                treatment.AdditionalInstructionItems.Add(new TreatmentAdditionalInstruction { AdditionalInstructionId = instructionId });
            foreach (var itemId in GetDiagnosticInvestigationIds(request))
                treatment.DiagnosticInvestigations.Add(new TreatmentDiagnosticInvestigation { DiagnosticInvestigationId = itemId });
        }

        private async Task<Treatment> LoadOwned(int id, CancellationToken cancellationToken)
            => await OwnedTreatments.AsNoTracking().AsSplitQuery().Include(t => t.Patient)
                .Include(t => t.PrescriptionItems).Include(t => t.ClinicalNotes).ThenInclude(l => l.ClinicalNote)
                .Include(t => t.AdditionalInstructionItems).ThenInclude(link => link.AdditionalInstruction)
                .Include(t => t.DiagnosticInvestigations).ThenInclude(link => link.DiagnosticInvestigation)
                .SingleAsync(t => t.Id == id, cancellationToken);

        private static TreatmentResponse ToResponse(Treatment treatment) => new(treatment.Id, treatment.RecordedAtUtc,
            new TreatmentPatientResponse(treatment.Patient.Id, treatment.Patient.Code, treatment.Patient.Name,
                treatment.Patient.Phone, treatment.Patient.Age, treatment.Patient.Gender, treatment.Patient.Address),
            treatment.BloodPressure, treatment.AdditionalInstructions, treatment.ClinicalNotes.OrderBy(n => n.ClinicalNoteId)
                .Select(n => new ClinicalNoteResponse(n.ClinicalNoteId, n.ClinicalNote.Text)).ToList(),
            treatment.AdditionalInstructionItems.OrderBy(item => item.AdditionalInstructionId)
                .Select(item => new AdditionalInstructionResponse(item.AdditionalInstructionId, item.AdditionalInstruction.Text)).ToList(),
            treatment.DiagnosticInvestigations.OrderBy(item => item.DiagnosticInvestigationId)
                .Select(item => new DiagnosticInvestigationResponse(item.DiagnosticInvestigationId, item.DiagnosticInvestigation.Text, item.DiagnosticInvestigation.Kind)).ToList(),
            treatment.DiagnosticInvestigations.Where(item => item.DiagnosticInvestigation.Kind == "Diagnostic").OrderBy(item => item.DiagnosticInvestigationId)
                .Select(item => new DiagnosticInvestigationResponse(item.DiagnosticInvestigationId, item.DiagnosticInvestigation.Text, item.DiagnosticInvestigation.Kind)).ToList(),
            treatment.DiagnosticInvestigations.Where(item => item.DiagnosticInvestigation.Kind == "Investigation").OrderBy(item => item.DiagnosticInvestigationId)
                .Select(item => new DiagnosticInvestigationResponse(item.DiagnosticInvestigationId, item.DiagnosticInvestigation.Text, item.DiagnosticInvestigation.Kind)).ToList(),
            treatment.PrescriptionItems.OrderBy(i => i.Position).Select(i => new PrescriptionItemResponse(i.Id, i.Position,
                i.MedicationId, i.MedicationName, i.MedicationType, i.Dose, i.Frequency, i.Duration)).ToList());
    }

    public record DashboardVisitDay(string Date, int Visits, int Prescriptions);
    public record DashboardSummaryResponse(int TotalPatients, int PatientsToday, int TreatmentsToday,
        int PrescriptionsToday, int TotalMedications, IReadOnlyList<DashboardVisitDay> Visits,
        IReadOnlyList<TreatmentSummaryResponse> RecentTreatments);
}
