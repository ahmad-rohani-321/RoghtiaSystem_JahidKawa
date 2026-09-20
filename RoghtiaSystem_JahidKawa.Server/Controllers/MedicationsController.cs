using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Data;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.Security.Claims;

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
            var medication = new Medication { UserId = LoggedInUserId };
            ApplyChanges(medication, request);
            context.Medications.Add(medication);
            await context.SaveChangesAsync(cancellationToken);
            var response = await ToResponses(OwnedMedications.AsNoTracking().Where(m => m.Id == medication.Id))
                .SingleAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = medication.Id }, response);
        }

        [HttpPut("{id:int}")]
        [Consumes("application/json")]
        [RequestSizeLimit(16 * 1024)]
        public async Task<IActionResult> Update(int id, [FromBody] MedicationRequest request, CancellationToken cancellationToken)
        {
            var medication = await OwnedMedications.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (medication == null) return MedicationNotFound();
            ApplyChanges(medication, request);
            await context.SaveChangesAsync(cancellationToken);
            var response = await ToResponses(OwnedMedications.AsNoTracking().Where(m => m.Id == id))
                .SingleAsync(cancellationToken);
            return Ok(response);
        }

        private NotFoundObjectResult MedicationNotFound()
            => NotFound(new AuthError("NOT_FOUND", "درمل ونه موندل شول."));

        private static void ApplyChanges(Medication medication, MedicationRequest request)
        {
            medication.Name = request.Name!.Trim();
            medication.Type = request.Type!.Trim();
            medication.Quantity = request.Quantity!.Value;
            medication.Remarks = request.Remarks?.Trim() ?? string.Empty;
        }

        private static IQueryable<MedicationResponse> ToResponses(IQueryable<Medication> medications)
            => medications.Select(m => new MedicationResponse(m.Id, m.Name, m.Type, m.Quantity, m.Remarks,
                new MedicationUserResponse(m.User.Id, m.User.UserName ?? string.Empty)));
    }
}
