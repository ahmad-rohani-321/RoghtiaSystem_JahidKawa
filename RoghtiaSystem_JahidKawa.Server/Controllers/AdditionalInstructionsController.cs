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
    [Route("api/additional-instructions")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AdditionalInstructionsController(MainDbContext context) : ControllerBase
    {
        private int LoggedInUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
            => Ok(await context.AdditionalInstructions.AsNoTracking().Where(item => item.UserId == LoggedInUserId)
                .OrderBy(item => item.Text).Select(item => new AdditionalInstructionResponse(item.Id, item.Text)).ToListAsync(cancellationToken));

        [HttpPost]
        [Consumes("application/json")]
        [RequestSizeLimit(2048)]
        public async Task<IActionResult> Create([FromBody] AdditionalInstructionRequest request, CancellationToken cancellationToken)
        {
            var text = request.Text!.Trim();
            if (text.Length == 0) return BadRequest(new AuthError("VALIDATION_ERROR", "Enter an instruction."));
            var existing = await context.AdditionalInstructions.AsNoTracking()
                .Where(item => item.UserId == LoggedInUserId && item.Text == text)
                .Select(item => new AdditionalInstructionResponse(item.Id, item.Text)).SingleOrDefaultAsync(cancellationToken);
            if (existing != null) return Ok(existing);
            var instruction = new AdditionalInstruction { UserId = LoggedInUserId, Text = text };
            context.AdditionalInstructions.Add(instruction);
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                var duplicate = await context.AdditionalInstructions.AsNoTracking()
                    .Where(item => item.UserId == LoggedInUserId && item.Text == text)
                    .Select(item => new AdditionalInstructionResponse(item.Id, item.Text)).SingleOrDefaultAsync(cancellationToken);
                if (duplicate != null) return Ok(duplicate);
                throw;
            }
            return Ok(new AdditionalInstructionResponse(instruction.Id, instruction.Text));
        }
    }
}
