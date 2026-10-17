using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Data;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace RoghtiaSystem_JahidKawa.Server.Controllers
{
    [Authorize]
    [Route("api/medications")]
    [ApiController]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class MedicationsController(MainDbContext context) : ControllerBase
    {
        // The verified principal owns every query and mutation; request data cannot choose an owner.
        private int LoggedInUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);

        private IQueryable<Medication> OwnedMedications => context.Medications.Where(m => m.UserId == LoggedInUserId);

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] MedicationQuery query, CancellationToken cancellationToken)
        {
            var medications = OwnedMedications.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                // SQLite LIKE ignores ASCII case; escaping metacharacters keeps searches literal.
                var search = query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                var pattern = $"%{search}%";
                medications = medications.Where(m => EF.Functions.Like(m.Name, pattern, "\\")
                    || EF.Functions.Like(m.Type, pattern, "\\")
                    || EF.Functions.Like(m.Remarks, pattern, "\\"));
            }

            var total = await medications.CountAsync(cancellationToken);
            var offset = ((long)query.Page - 1) * query.PageSize;
            // Compare before converting the offset, so even very large valid page numbers are safe.
            List<MedicationResponse> items = offset >= total
                ? []
                : await ToResponses(medications.OrderByDescending(m => m.Id).Skip((int)offset).Take(query.PageSize))
                    .ToListAsync(cancellationToken);
            return Ok(new MedicationListResponse(items, total, query.Page, query.PageSize));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var medication = await ToResponses(OwnedMedications.AsNoTracking().Where(m => m.Id == id))
                .SingleOrDefaultAsync(cancellationToken);
            return medication == null ? MedicationNotFound() : Ok(medication);
        }

        [HttpPost]
        [Consumes("application/json")]
        [RequestSizeLimit(16 * 1024)]
        public async Task<IActionResult> Create([FromBody] MedicationRequest request, CancellationToken cancellationToken)
        {
            if (await HasDuplicateName(request.Name!, null, cancellationToken))
                return DuplicateMedicationName();

            var medication = new Medication { UserId = LoggedInUserId };
            ApplyChanges(medication, request);
            context.Medications.Add(medication);
            await context.SaveChangesAsync(cancellationToken);
            var response = await ToResponses(OwnedMedications.AsNoTracking().Where(m => m.Id == medication.Id))
                .SingleAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = medication.Id }, response);
        }

        [HttpPost("bulk")]
        [Consumes("application/json")]
        [RequestSizeLimit(512 * 1024)]
        public async Task<IActionResult> CreateBulk([FromBody] BulkMedicationRequest request, CancellationToken cancellationToken)
        {
            if (request.Items == null || request.Items.Count is < 1 or > 50)
                return BadRequest(new AuthError("VALIDATION_ERROR", "Add between 1 and 50 medications."));

            foreach (var item in request.Items)
            {
                if (item == null)
                    return BadRequest(new AuthError("VALIDATION_ERROR", "Check each medication row and try again."));
                var validationResults = new List<ValidationResult>();
                if (!Validator.TryValidateObject(item, new ValidationContext(item), validationResults, validateAllProperties: true))
                    return BadRequest(new AuthError("VALIDATION_ERROR", "Check each medication row and try again."));
            }

            var existingNames = await OwnedMedications.AsNoTracking().Select(item => item.Name).ToListAsync(cancellationToken);
            var names = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
            foreach (var item in request.Items)
            {
                if (!names.Add(item.Name!.Trim()))
                    return DuplicateMedicationName();
            }

            var medications = request.Items.Select(item =>
            {
                var medication = new Medication { UserId = LoggedInUserId };
                ApplyChanges(medication, item);
                return medication;
            }).ToList();

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.Medications.AddRange(medications);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var ids = medications.Select(item => item.Id).ToHashSet();
            var response = await ToResponses(OwnedMedications.AsNoTracking().Where(item => ids.Contains(item.Id)))
                .ToListAsync(cancellationToken);
            var byId = response.ToDictionary(item => item.Id);
            return Ok(medications.Select(item => byId[item.Id]).ToList());
        }

        [HttpPut("{id:int}")]
        [Consumes("application/json")]
        [RequestSizeLimit(16 * 1024)]
        public async Task<IActionResult> Update(int id, [FromBody] MedicationRequest request, CancellationToken cancellationToken)
        {
            var medication = await OwnedMedications.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (medication == null) return MedicationNotFound();
            if (await HasDuplicateName(request.Name!, id, cancellationToken))
                return DuplicateMedicationName();
            ApplyChanges(medication, request);
            await context.SaveChangesAsync(cancellationToken);
            var response = await ToResponses(OwnedMedications.AsNoTracking().Where(m => m.Id == id))
                .SingleAsync(cancellationToken);
            return Ok(response);
        }

        private NotFoundObjectResult MedicationNotFound()
            => NotFound(new AuthError("NOT_FOUND", "Medication not found."));

        private ConflictObjectResult DuplicateMedicationName()
            => Conflict(new AuthError("DUPLICATE_MEDICATION", "A medication with this name already exists in your list."));

        private async Task<bool> HasDuplicateName(string name, int? excludedId, CancellationToken cancellationToken)
        {
            var names = await OwnedMedications.AsNoTracking()
                .Where(item => excludedId == null || item.Id != excludedId.Value)
                .Select(item => item.Name)
                .ToListAsync(cancellationToken);
            return names.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        private static void ApplyChanges(Medication medication, MedicationRequest request)
        {
            medication.Name = request.Name!.Trim();
            medication.Type = request.Type!.Trim();
            medication.Quantity = request.Quantity ?? 0;
            medication.Remarks = request.Remarks?.Trim() ?? string.Empty;
        }

        private static IQueryable<MedicationResponse> ToResponses(IQueryable<Medication> medications)
            => medications.Select(m => new MedicationResponse(m.Id, m.Name, m.Type, m.Quantity, m.Remarks,
                new MedicationUserResponse(m.User.Id, m.User.UserName ?? string.Empty)));
    }
}
