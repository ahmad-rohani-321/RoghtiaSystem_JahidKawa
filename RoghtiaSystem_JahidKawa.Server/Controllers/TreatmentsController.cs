using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Data;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.Globalization;
using System.Security.Claims;

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

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] TreatmentQuery query, CancellationToken cancellationToken)
        {
            if (query.FromUtc.HasValue != query.ToUtc.HasValue ||
                (query.FromUtc.HasValue && (query.FromUtc.Value.Kind != DateTimeKind.Utc || query.ToUtc!.Value.Kind != DateTimeKind.Utc ||
                    query.FromUtc >= query.ToUtc || query.ToUtc - query.FromUtc > TimeSpan.FromDays(32))))
                return BadRequest(new AuthError("VALIDATION_ERROR", "د نېټې ټاکنه سمه نه ده."));

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

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var treatment = await OwnedTreatments.AsNoTracking().AsSplitQuery()
                .Include(t => t.Patient).Include(t => t.PrescriptionItems).Include(t => t.ClinicalNotes).ThenInclude(l => l.ClinicalNote)
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
            return treatment == null ? NotFound(new AuthError("NOT_FOUND", "تداوي ونه موندل شوه.")) : Ok(ToResponse(treatment));
        }

        [HttpPost]
        [Consumes("application/json")]
        [RequestSizeLimit(32 * 1024)]
        public async Task<IActionResult> Create([FromBody] TreatmentRequest request, CancellationToken cancellationToken)
        {
            var (error, medications) = await ValidateReferences(request, cancellationToken);
            if (error != null) return error;
            var treatment = new Treatment { UserId = LoggedInUserId, PatientId = request.PatientId!.Value,
                RecordedAtUtc = DateTime.UtcNow, BloodPressure = request.BloodPressure?.Trim() ?? string.Empty };
            ApplyChanges(treatment, request, medications);
            context.Treatments.Add(treatment);
            await context.SaveChangesAsync(cancellationToken);
            var saved = await LoadOwned(treatment.Id, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = saved.Id }, ToResponse(saved));
        }

        [HttpPut("{id:int}")]
        [Consumes("application/json")]
        [RequestSizeLimit(32 * 1024)]
        public async Task<IActionResult> Update(int id, [FromBody] TreatmentRequest request, CancellationToken cancellationToken)
        {
            var treatment = await OwnedTreatments.Include(t => t.PrescriptionItems).Include(t => t.ClinicalNotes)
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (treatment == null) return NotFound(new AuthError("NOT_FOUND", "تداوي ونه موندل شوه."));
            var (error, medications) = await ValidateReferences(request, cancellationToken);
            if (error != null) return error;

            // Removing old rows before inserting their replacements avoids the unique position index.
            // Both saves are in one transaction, so an invalid replacement never erases a prescription.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.PrescriptionItems.RemoveRange(treatment.PrescriptionItems);
            context.TreatmentClinicalNotes.RemoveRange(treatment.ClinicalNotes);
            await context.SaveChangesAsync(cancellationToken);
            treatment.PrescriptionItems.Clear();
            treatment.ClinicalNotes.Clear();
            treatment.PatientId = request.PatientId!.Value;
            treatment.BloodPressure = request.BloodPressure?.Trim() ?? string.Empty;
            ApplyChanges(treatment, request, medications);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(ToResponse(await LoadOwned(id, cancellationToken)));
        }

        private async Task<(IActionResult? Error, Dictionary<int, Medication> Medications)> ValidateReferences(
            TreatmentRequest request, CancellationToken cancellationToken)
        {
            var ownerId = LoggedInUserId;
            if (!await context.Patients.AsNoTracking().AnyAsync(p => p.Id == request.PatientId && p.UserId == ownerId, cancellationToken))
                return (BadRequest(new AuthError("VALIDATION_ERROR", "خپل ناروغ وټاکئ.")), []);
            var noteIds = request.ClinicalNoteIds!;
            if (noteIds.Any(id => id < 1) || noteIds.Distinct().Count() != noteIds.Count ||
                await context.ClinicalNotes.CountAsync(n => n.UserId == ownerId && noteIds.Contains(n.Id), cancellationToken) != noteIds.Count)
                return (BadRequest(new AuthError("VALIDATION_ERROR", "کلینیکي یادښتونه سم نه دي.")), []);
            var items = request.PrescriptionItems!;
            if (items.Any(item => string.IsNullOrWhiteSpace(item.Duration)))
                return (BadRequest(new AuthError("VALIDATION_ERROR", "د درملو موده ولیکئ.")), []);
            var medicationIds = items.Select(item => item.MedicationId!.Value).Distinct().ToList();
            var medications = await context.Medications.AsNoTracking().Where(m => m.UserId == ownerId && medicationIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, cancellationToken);
            if (medications.Count != medicationIds.Count)
                return (BadRequest(new AuthError("VALIDATION_ERROR", "خپل ثبت شوي درمل وټاکئ.")), []);
            return (null, medications);
        }

        private static void ApplyChanges(Treatment treatment, TreatmentRequest request, Dictionary<int, Medication> medications)
        {
            var items = request.PrescriptionItems!;
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var medication = medications[item.MedicationId!.Value];
                treatment.PrescriptionItems.Add(new PrescriptionItem { Position = index + 1, MedicationId = medication.Id,
                    MedicationName = medication.Name, MedicationType = medication.Type, Duration = item.Duration!.Trim(), Refills = item.Refills!.Value });
            }
            foreach (var noteId in request.ClinicalNoteIds!) treatment.ClinicalNotes.Add(new TreatmentClinicalNote { ClinicalNoteId = noteId });
        }

        private async Task<Treatment> LoadOwned(int id, CancellationToken cancellationToken)
            => await OwnedTreatments.AsNoTracking().AsSplitQuery().Include(t => t.Patient)
                .Include(t => t.PrescriptionItems).Include(t => t.ClinicalNotes).ThenInclude(l => l.ClinicalNote)
                .SingleAsync(t => t.Id == id, cancellationToken);

        private static TreatmentResponse ToResponse(Treatment treatment) => new(treatment.Id, treatment.RecordedAtUtc,
            new TreatmentPatientResponse(treatment.Patient.Id, treatment.Patient.Code, treatment.Patient.Name,
                treatment.Patient.Phone, treatment.Patient.Age, treatment.Patient.Gender, treatment.Patient.Address),
            treatment.BloodPressure, treatment.ClinicalNotes.OrderBy(n => n.ClinicalNoteId)
                .Select(n => new ClinicalNoteResponse(n.ClinicalNoteId, n.ClinicalNote.Text)).ToList(),
            treatment.PrescriptionItems.OrderBy(i => i.Position).Select(i => new PrescriptionItemResponse(i.Id, i.Position,
                i.MedicationId, i.MedicationName, i.MedicationType, i.Duration, i.Refills)).ToList());
    }
}
