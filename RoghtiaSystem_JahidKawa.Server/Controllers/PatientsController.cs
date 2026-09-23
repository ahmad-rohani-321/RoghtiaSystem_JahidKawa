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
    [Route("api/patients")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class PatientsController(MainDbContext context) : ControllerBase
    {
        private int LoggedInUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        private IQueryable<Patient> OwnedPatients => context.Patients.Where(p => p.UserId == LoggedInUserId);

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] PatientQuery query, CancellationToken cancellationToken)
        {
            var patients = OwnedPatients.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                var pattern = $"%{search}%";
                patients = patients.Where(p => EF.Functions.Like(p.Name, pattern, "\\")
                    || EF.Functions.Like(p.Code, pattern, "\\") || EF.Functions.Like(p.Phone, pattern, "\\"));
            }
            var total = await patients.CountAsync(cancellationToken);
            var offset = ((long)query.Page - 1) * query.PageSize;
            List<PatientResponse> items = offset >= total ? [] : await ToResponses(patients.OrderByDescending(p => p.Id)
                .Skip((int)offset).Take(query.PageSize)).ToListAsync(cancellationToken);
            return Ok(new PatientListResponse(items, total, query.Page, query.PageSize));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var patient = await ToResponses(OwnedPatients.AsNoTracking().Where(p => p.Id == id)).SingleOrDefaultAsync(cancellationToken);
            return patient == null ? PatientNotFound() : Ok(patient);
        }

        [HttpPost]
        [Consumes("application/json")]
        [RequestSizeLimit(16 * 1024)]
        public async Task<IActionResult> Create([FromBody] PatientRequest request, CancellationToken cancellationToken)
        {
            // SQLite's non-deferred transaction acquires the write lock before counting.
            // Concurrent creates therefore receive distinct sequential codes, even across API processes.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var next = await OwnedPatients.LongCountAsync(cancellationToken) + 1;
            var patient = new Patient { UserId = LoggedInUserId, Code = "RT-" + next.ToString(CultureInfo.InvariantCulture) };
            ApplyChanges(patient, request);
            context.Patients.Add(patient);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var response = await ToResponses(OwnedPatients.AsNoTracking().Where(p => p.Id == patient.Id)).SingleAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = patient.Id }, response);
        }

        [HttpPut("{id:int}")]
        [Consumes("application/json")]
        [RequestSizeLimit(16 * 1024)]
        public async Task<IActionResult> Update(int id, [FromBody] PatientRequest request, CancellationToken cancellationToken)
        {
            var patient = await OwnedPatients.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (patient == null) return PatientNotFound();
            ApplyChanges(patient, request);
            await context.SaveChangesAsync(cancellationToken);
            return Ok(await ToResponses(OwnedPatients.AsNoTracking().Where(p => p.Id == id)).SingleAsync(cancellationToken));
        }

        private NotFoundObjectResult PatientNotFound() => NotFound(new AuthError("NOT_FOUND", "ناروغ ونه موندل شو."));
        private static void ApplyChanges(Patient patient, PatientRequest request)
        {
            patient.Name = request.Name!.Trim();
            patient.Phone = request.Phone?.Trim() ?? string.Empty;
            patient.Age = request.Age!.Value;
            patient.Gender = request.Gender!;
            patient.Address = request.Address?.Trim() ?? string.Empty;
        }
        private static IQueryable<PatientResponse> ToResponses(IQueryable<Patient> patients)
            => patients.Select(p => new PatientResponse(p.Id, p.Code, p.Name, p.Phone, p.Age, p.Gender, p.Address,
                new PatientUserResponse(p.User.Id, p.User.UserName ?? string.Empty)));
    }
}
