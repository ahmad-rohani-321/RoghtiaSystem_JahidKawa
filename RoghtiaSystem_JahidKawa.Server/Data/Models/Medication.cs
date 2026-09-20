namespace RoghtiaSystem_JahidKawa.Server.Data.Models
{
    public class Medication
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public int UserId { get; set; }
        public Users User { get; set; } = null!;
    }
}
