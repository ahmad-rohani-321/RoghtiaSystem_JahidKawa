namespace RoghtiaSystem_JahidKawa.Server.Data.Models
{
    public class DoctorInformation
    {
        public string DoctorNameEnglish { get; set; } = string.Empty;
        public string DoctorNamePashto { get; set; } = string.Empty;
        public string DoctorProfessionPashto { get; set; } = string.Empty;
        public string DoctorProfessionEnglish { get; set; } = string.Empty;
        public byte[]? DoctorPhoto { get; set; }
        public byte[]? DoctorLogo { get; set; }
        public byte[]? HospitalLogo { get; set; }
        public string HospitalNamePashto { get; set; } = string.Empty;
        public string HospitalAddress { get; set; } = string.Empty;
        public string HospitalPhoneNumbers { get; set; } = string.Empty;
        public bool PrintOnA5 { get; set; }
        public bool PrintGrayscale { get; set; }
        public int LoggedInUserId { get; set; }
    }
}
