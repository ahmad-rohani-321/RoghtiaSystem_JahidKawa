using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Data;
using RoghtiaSystem_JahidKawa.Server.Data.Models;
using RoghtiaSystem_JahidKawa.Server.Services;
using RoghtiaSystem_JahidKawa.Server.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;

namespace RoghtiaSystem_JahidKawa.Server.Controllers
{
    [Authorize]
    [Route("api/doctor-information")]
    [ApiController]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class DoctorInformationController(MainDbContext context) : ControllerBase
    {
        // Ownership always comes from the verified principal; forms and route parameters cannot override it.
        private int LoggedInUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var doctor = await context.DoctorInformation.AsNoTracking()
                .SingleOrDefaultAsync(d => d.LoggedInUserId == LoggedInUserId, cancellationToken);
            return Ok(ToResponse(doctor));
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(DoctorImages.MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = DoctorImages.MaxRequestBytes)]
        public Task<IActionResult> Create([FromForm] DoctorInformationForm form, CancellationToken cancellationToken)
            => SaveAsync(form, true, cancellationToken);

        [HttpPut]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(DoctorImages.MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = DoctorImages.MaxRequestBytes)]
        public Task<IActionResult> Update([FromForm] DoctorInformationForm form, CancellationToken cancellationToken)
            => SaveAsync(form, false, cancellationToken);

        private async Task<IActionResult> SaveAsync(DoctorInformationForm form, bool create, CancellationToken cancellationToken)
        {
            var doctor = await context.DoctorInformation.SingleOrDefaultAsync(d => d.LoggedInUserId == LoggedInUserId, cancellationToken);
            if (create && doctor != null)
                return Conflict(new AuthError("DOCTOR_INFORMATION_EXISTS", "ستاسو معلومات مخکې ثبت شوي دي. مهرباني وکړئ پاڼه تازه کړئ."));
            if (!create && doctor == null)
                return NotFound(new AuthError("NOT_FOUND", "ستاسو معلومات ونه موندل شول. مهرباني وکړئ پاڼه تازه کړئ."));
            if ((form.RemoveDoctorPhoto && form.DoctorPhoto != null)
                || (form.RemoveDoctorLogo && form.DoctorLogo != null)
                || (form.RemoveHospitalLogo && form.HospitalLogo != null))
                return BadRequest(new AuthError("VALIDATION_ERROR", "یو انځور په یو وخت کې نه شي بدل او لرې کېدای."));

            byte[]? doctorPhoto, doctorLogo, hospitalLogo;
            try
            {
                // Validate all uploads before changing any fields, so a failed save leaves the record untouched.
                doctorPhoto = await DoctorImages.ReadAsync(form.DoctorPhoto, cancellationToken);
                doctorLogo = await DoctorImages.ReadAsync(form.DoctorLogo, cancellationToken);
                hospitalLogo = await DoctorImages.ReadAsync(form.HospitalLogo, cancellationToken);
            }
            catch (ImageValidationException exception)
            {
                return StatusCode(exception.Code == "IMAGE_TOO_LARGE" ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest,
                    new AuthError(exception.Code, exception.Message));
            }

            if (doctor == null)
            {
                doctor = new DoctorInformation { LoggedInUserId = LoggedInUserId };
                context.DoctorInformation.Add(doctor);
            }
            doctor.DoctorNameEnglish = form.DoctorNameEnglish?.Trim() ?? string.Empty;
            doctor.DoctorNamePashto = form.DoctorNamePashto?.Trim() ?? string.Empty;
            doctor.DoctorProfessionPashto = form.DoctorProfessionPashto?.Trim() ?? string.Empty;
            doctor.DoctorProfessionEnglish = form.DoctorProfessionEnglish?.Trim() ?? string.Empty;
            doctor.HospitalNamePashto = form.HospitalNamePashto?.Trim() ?? string.Empty;
            doctor.DoctorPhoto = form.RemoveDoctorPhoto ? null : doctorPhoto ?? doctor.DoctorPhoto;
            doctor.DoctorLogo = form.RemoveDoctorLogo ? null : doctorLogo ?? doctor.DoctorLogo;
            doctor.HospitalLogo = form.RemoveHospitalLogo ? null : hospitalLogo ?? doctor.HospitalLogo;

            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException exception) when (create && exception.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                // The owner primary key also protects against concurrent first saves.
                return Conflict(new AuthError("DOCTOR_INFORMATION_EXISTS", "ستاسو معلومات مخکې ثبت شوي دي. مهرباني وکړئ پاڼه تازه کړئ."));
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound(new AuthError("NOT_FOUND", "ستاسو معلومات بدل شوي دي. مهرباني وکړئ پاڼه تازه کړئ."));
            }
            return Ok(ToResponse(doctor));
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(CancellationToken cancellationToken)
        {
            await DeleteCurrentAsync(cancellationToken);
            return NoContent();
        }

        [HttpPost("reset")]
        public async Task<IActionResult> Reset(CancellationToken cancellationToken)
        {
            await DeleteCurrentAsync(cancellationToken);
            return Ok(ToResponse(null));
        }

        private Task<int> DeleteCurrentAsync(CancellationToken cancellationToken)
            => context.DoctorInformation.Where(d => d.LoggedInUserId == LoggedInUserId).ExecuteDeleteAsync(cancellationToken);

        [HttpGet("media/{kind}")]
        public async Task<IActionResult> Media(string kind, CancellationToken cancellationToken)
        {
            var owned = context.DoctorInformation.AsNoTracking().Where(d => d.LoggedInUserId == LoggedInUserId);
            byte[]? bytes = kind switch
            {
                "doctor-photo" => await owned.Select(d => d.DoctorPhoto).SingleOrDefaultAsync(cancellationToken),
                "doctor-logo" => await owned.Select(d => d.DoctorLogo).SingleOrDefaultAsync(cancellationToken),
                "hospital-logo" => await owned.Select(d => d.HospitalLogo).SingleOrDefaultAsync(cancellationToken),
                _ => null
            };
            if (bytes == null || DoctorImages.GetContentType(bytes) is not { } contentType)
                return NotFound(new AuthError("NOT_FOUND", "انځور ونه موندل شو."));
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(bytes, contentType);
        }

        private DoctorInformationResponse ToResponse(DoctorInformation? doctor) => new(
            doctor?.DoctorNameEnglish ?? string.Empty,
            doctor?.DoctorNamePashto ?? string.Empty,
            doctor?.DoctorProfessionPashto ?? string.Empty,
            doctor?.DoctorProfessionEnglish ?? string.Empty,
            MediaUrl(doctor?.DoctorPhoto, "doctor-photo"),
            MediaUrl(doctor?.DoctorLogo, "doctor-logo"),
            MediaUrl(doctor?.HospitalLogo, "hospital-logo"),
            doctor?.HospitalNamePashto ?? string.Empty,
            LoggedInUserId,
            doctor != null);

        private static string? MediaUrl(byte[]? bytes, string kind)
            => bytes == null ? null : $"/api/doctor-information/media/{kind}?v={Convert.ToHexStringLower(SHA256.HashData(bytes))[..16]}";
    }
}
