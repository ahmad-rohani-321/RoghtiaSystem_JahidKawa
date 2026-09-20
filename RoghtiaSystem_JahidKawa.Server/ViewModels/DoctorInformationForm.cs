using System.ComponentModel.DataAnnotations;

namespace RoghtiaSystem_JahidKawa.Server.ViewModels
{
    public class DoctorInformationForm
    {
        [StringLength(160)]
        public string? DoctorNameEnglish { get; set; }
        [StringLength(160)]
        public string? DoctorNamePashto { get; set; }
        [StringLength(200)]
        public string? DoctorProfessionPashto { get; set; }
        [StringLength(200)]
        public string? DoctorProfessionEnglish { get; set; }
        [StringLength(200)]
        public string? HospitalNamePashto { get; set; }
        public IFormFile? DoctorPhoto { get; set; }
        public IFormFile? DoctorLogo { get; set; }
        public IFormFile? HospitalLogo { get; set; }
        public bool RemoveDoctorPhoto { get; set; }
        public bool RemoveDoctorLogo { get; set; }
        public bool RemoveHospitalLogo { get; set; }
    }

    public record DoctorInformationResponse(
        string DoctorNameEnglish,
        string DoctorNamePashto,
        string DoctorProfessionPashto,
        string DoctorProfessionEnglish,
        string? DoctorPhoto,
        string? DoctorLogo,
        string? HospitalLogo,
        string HospitalNamePashto,
        int LoggedInUserId,
        bool Exists);
}
