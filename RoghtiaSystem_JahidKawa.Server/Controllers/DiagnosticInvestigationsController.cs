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
    [Route("api/diagnostics-investigations")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class DiagnosticInvestigationsController(MainDbContext context) : ControllerBase
    {
        private int LoggedInUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string kind, CancellationToken cancellationToken)
        {
            if (!IsValidKind(kind)) return BadRequest(new AuthError("VALIDATION_ERROR", "Choose diagnostics or investigations."));
            return Ok(await context.DiagnosticInvestigations.AsNoTracking()
                .Where(item => item.UserId == LoggedInUserId && item.Kind == kind)
                .OrderBy(item => item.Text).Select(item => new DiagnosticInvestigationResponse(item.Id, item.Text, item.Kind)).ToListAsync(cancellationToken));
        }

        [HttpPost]
        [Consumes("application/json")]
        [RequestSizeLimit(2048)]
        public async Task<IActionResult> Create([FromBody] DiagnosticInvestigationRequest request, CancellationToken cancellationToken)
        {
            var text = request.Text!.Trim();
            var kind = request.Kind!;
            if (!IsValidKind(kind) || text.Length == 0) return BadRequest(new AuthError("VALIDATION_ERROR", "Enter a valid diagnostic or investigation."));
            var existing = await context.DiagnosticInvestigations.AsNoTracking()
                .Where(item => item.UserId == LoggedInUserId && item.Kind == kind && item.Text == text)
                .Select(item => new DiagnosticInvestigationResponse(item.Id, item.Text, item.Kind)).SingleOrDefaultAsync(cancellationToken);
            if (existing != null) return Ok(existing);
            var item = new DiagnosticInvestigation { UserId = LoggedInUserId, Kind = kind, Text = text };
            context.DiagnosticInvestigations.Add(item);
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                var duplicate = await context.DiagnosticInvestigations.AsNoTracking()
                    .Where(value => value.UserId == LoggedInUserId && value.Kind == kind && value.Text == text)
                    .Select(value => new DiagnosticInvestigationResponse(value.Id, value.Text, value.Kind)).SingleOrDefaultAsync(cancellationToken);
                if (duplicate != null) return Ok(duplicate);
                throw;
            }
            return Ok(new DiagnosticInvestigationResponse(item.Id, item.Text, item.Kind));
        }

        private static bool IsValidKind(string? kind)
            => kind is "Diagnostic" or "Investigation";
    }
}
