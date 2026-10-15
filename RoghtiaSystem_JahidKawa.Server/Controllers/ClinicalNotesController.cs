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
    [Route("api/clinical-notes")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ClinicalNotesController(MainDbContext context) : ControllerBase
    {
        private int LoggedInUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
            => Ok(await context.ClinicalNotes.AsNoTracking().Where(n => n.UserId == LoggedInUserId)
                .OrderBy(n => n.Text).Select(n => new ClinicalNoteResponse(n.Id, n.Text)).ToListAsync(cancellationToken));

        [HttpPost]
        [Consumes("application/json")]
        [RequestSizeLimit(2048)]
        public async Task<IActionResult> Create([FromBody] ClinicalNoteRequest request, CancellationToken cancellationToken)
        {
            var text = request.Text!.Trim();
            if (text.Length == 0) return BadRequest(new AuthError("VALIDATION_ERROR", "Enter a note."));
            var existing = await context.ClinicalNotes.AsNoTracking().Where(n => n.UserId == LoggedInUserId && n.Text == text)
                .Select(n => new ClinicalNoteResponse(n.Id, n.Text)).SingleOrDefaultAsync(cancellationToken);
            if (existing != null) return Ok(existing);
            var note = new ClinicalNote { UserId = LoggedInUserId, Text = text };
            context.ClinicalNotes.Add(note);
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                var duplicate = await context.ClinicalNotes.AsNoTracking().Where(n => n.UserId == LoggedInUserId && n.Text == text)
                    .Select(n => new ClinicalNoteResponse(n.Id, n.Text)).SingleOrDefaultAsync(cancellationToken);
                if (duplicate != null) return Ok(duplicate);
                throw;
            }
            return Ok(new ClinicalNoteResponse(note.Id, note.Text));
        }
    }
}
