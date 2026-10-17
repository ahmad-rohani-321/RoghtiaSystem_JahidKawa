using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Data;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.Globalization;
using System.Security.Claims;

namespace RoghtiaSystem_JahidKawa.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/reports")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ReportsController(MainDbContext context) : ControllerBase
    {
        private static readonly TimeSpan KabulOffset = TimeSpan.FromMinutes(270);
        private int LoggedInUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
        {
            if (!fromUtc.HasValue || !toUtc.HasValue || fromUtc.Value.Kind != DateTimeKind.Utc || toUtc.Value.Kind != DateTimeKind.Utc ||
                fromUtc.Value >= toUtc.Value || toUtc.Value - fromUtc.Value > TimeSpan.FromDays(367))
                return BadRequest(new AuthError("VALIDATION_ERROR", "Select a valid report period of up to one year."));

            var ownerId = LoggedInUserId;
            var start = fromUtc.Value;
            var end = toUtc.Value;
            var treatments = context.Treatments.AsNoTracking()
                .Where(item => item.UserId == ownerId && item.RecordedAtUtc >= start && item.RecordedAtUtc < end);

            var treatmentTotal = await treatments.CountAsync(cancellationToken);
            var withPrescription = await treatments.CountAsync(item => item.PrescriptionItems.Any(), cancellationToken);
            var recordedAt = await treatments.Select(item => item.RecordedAtUtc).ToListAsync(cancellationToken);
            var byDay = recordedAt
                .GroupBy(value => (value + KabulOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Select(group => new DailyTreatmentReportItem(group.Key, group.Count(), 0))
                .ToList();

            var patientVisits = await treatments.GroupBy(item => item.PatientId)
                .Select(group => new { PatientId = group.Key, Visits = group.Count() })
                .ToListAsync(cancellationToken);
            var visitCounts = patientVisits.ToDictionary(item => item.PatientId, item => item.Visits);
            var patients = await context.Patients.AsNoTracking().Where(item => item.UserId == ownerId)
                .OrderBy(item => item.Name).ThenBy(item => item.Id)
                .Select(item => new { item.Id, item.Code, item.Name, item.Phone, item.Age, item.Gender, item.Address })
                .ToListAsync(cancellationToken);
            var patientReport = patients.Select(item => new PatientReportItem(item.Id, item.Code, item.Name, item.Phone,
                item.Age, item.Gender, item.Address, visitCounts.GetValueOrDefault(item.Id))).ToList();
            var mostVisited = patientReport.Where(item => item.Visits > 0).OrderByDescending(item => item.Visits).ThenBy(item => item.Name)
                .Take(20).Select(item => new PatientVisitReportItem(item.Id, item.Code, item.Name, item.Visits)).ToList();

            var prescriptionItems = context.PrescriptionItems.AsNoTracking()
                .Where(item => item.Treatment.UserId == ownerId && item.Treatment.RecordedAtUtc >= start && item.Treatment.RecordedAtUtc < end);
            var medicationUsage = await prescriptionItems.GroupBy(item => new { item.MedicationId, item.MedicationName, item.MedicationType })
                .Select(group => new
                {
                    group.Key.MedicationId,
                    group.Key.MedicationName,
                    group.Key.MedicationType,
                    Lines = group.Count(),
                    Dose = group.Sum(item => (long)item.Dose),
                    DeductedDose = group.Where(item => item.StockDeducted).Sum(item => (long)item.Dose)
                }).ToListAsync(cancellationToken);
            var usageByMedication = medicationUsage.GroupBy(item => item.MedicationId)
                .ToDictionary(group => group.Key, group => new
                {
                    Lines = group.Sum(item => item.Lines),
                    Dose = group.Sum(item => item.Dose),
                    DeductedDose = group.Sum(item => item.DeductedDose)
                });
            var inventory = await context.Medications.AsNoTracking().Where(item => item.UserId == ownerId)
                .OrderBy(item => item.Name).ThenBy(item => item.Id)
                .Select(item => new { item.Id, item.Name, item.Type, item.Quantity })
                .ToListAsync(cancellationToken);
            var medicationReport = inventory.Select(item =>
            {
                var usage = usageByMedication.GetValueOrDefault(item.Id);
                return new MedicationReportItem(item.Id, item.Name, item.Type, item.Quantity,
                    usage?.Lines ?? 0, usage?.Dose ?? 0, usage?.DeductedDose ?? 0);
            }).ToList();

            var noteUses = await context.TreatmentClinicalNotes.AsNoTracking()
                .Where(link => link.Treatment.UserId == ownerId && link.Treatment.RecordedAtUtc >= start && link.Treatment.RecordedAtUtc < end && link.ClinicalNote.UserId == ownerId)
                .GroupBy(link => link.ClinicalNoteId).Select(group => new { Id = group.Key, Uses = group.Count() })
                .ToListAsync(cancellationToken);
            var noteUseById = noteUses.ToDictionary(item => item.Id, item => item.Uses);
            var notes = await context.ClinicalNotes.AsNoTracking().Where(item => item.UserId == ownerId)
                .OrderBy(item => item.Text).ThenBy(item => item.Id).Select(item => new { item.Id, item.Text }).ToListAsync(cancellationToken);
            var noteReport = notes.Select(item => new TextUsageReportItem(item.Text, noteUseById.GetValueOrDefault(item.Id))).ToList();

            var instructionUses = await context.TreatmentAdditionalInstructions.AsNoTracking()
                .Where(link => link.Treatment.UserId == ownerId && link.Treatment.RecordedAtUtc >= start && link.Treatment.RecordedAtUtc < end && link.AdditionalInstruction.UserId == ownerId)
                .GroupBy(link => link.AdditionalInstructionId).Select(group => new { Id = group.Key, Uses = group.Count() })
                .ToListAsync(cancellationToken);
            var instructionUseById = instructionUses.ToDictionary(item => item.Id, item => item.Uses);
            var instructions = await context.AdditionalInstructions.AsNoTracking().Where(item => item.UserId == ownerId)
                .OrderBy(item => item.Text).ThenBy(item => item.Id).Select(item => new { item.Id, item.Text }).ToListAsync(cancellationToken);
            var instructionReport = instructions.Select(item => new TextUsageReportItem(item.Text, instructionUseById.GetValueOrDefault(item.Id))).ToList();

            // Prescription counts per day are grouped by the same Kabul-local calendar day as treatment counts.
            var prescribedAt = await treatments.Where(item => item.PrescriptionItems.Any())
                .Select(item => item.RecordedAtUtc).ToListAsync(cancellationToken);
            var prescriptionsByDay = prescribedAt.GroupBy(value => (value + KabulOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToDictionary(group => group.Key, group => group.Count());
            byDay = byDay.Select(item => item with { Prescriptions = prescriptionsByDay.GetValueOrDefault(item.Date) })
                .OrderBy(item => item.Date).ToList();

            return Ok(new ReportsResponse(start, end,
                new TreatmentReportResponse(treatmentTotal, withPrescription, byDay, mostVisited),
                medicationReport, patientReport, noteReport, instructionReport));
        }
    }
}
