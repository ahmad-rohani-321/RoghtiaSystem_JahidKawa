using Microsoft.EntityFrameworkCore;
using RoghtiaSystem_JahidKawa.Server.Data.Models;

namespace RoghtiaSystem_JahidKawa.Server.Data
{
    public class MainDbContext : DbContext
    {
        public MainDbContext(DbContextOptions<MainDbContext> options) : base(options)
        {
        }

        public DbSet<Users> Users { get; set; }
        public DbSet<DoctorInformation> DoctorInformation { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Treatment> Treatments { get; set; }
        public DbSet<PrescriptionItem> PrescriptionItems { get; set; }
        public DbSet<ClinicalNote> ClinicalNotes { get; set; }
        public DbSet<TreatmentClinicalNote> TreatmentClinicalNotes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Patient>(patient =>
            {
                patient.Property(p => p.Code).IsRequired().HasMaxLength(32);
                patient.Property(p => p.Name).IsRequired().HasMaxLength(200);
                patient.Property(p => p.Phone).IsRequired().HasMaxLength(32);
                patient.Property(p => p.Gender).IsRequired().HasMaxLength(16);
                patient.Property(p => p.Address).IsRequired().HasMaxLength(1000);
                patient.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
                patient.HasIndex(p => new { p.UserId, p.Code }).IsUnique();
                patient.HasIndex(p => new { p.UserId, p.Id });
                patient.ToTable(table => table.HasCheckConstraint("CK_Patients_Age", "Age BETWEEN 0 AND 150"));
            });
            modelBuilder.Entity<Users>(user =>
            {
                user.Property(u => u.UserName).IsRequired().HasMaxLength(64);
                user.Property(u => u.NormalizedUserName).IsRequired().HasMaxLength(64);
                user.HasIndex(u => u.NormalizedUserName).IsUnique();
                user.Property(u => u.Role).IsRequired().HasMaxLength(32).HasDefaultValue("User");
                user.Property(u => u.PINCode).IsRequired().HasMaxLength(512).HasDefaultValue(string.Empty);
                user.Property(u => u.TokenVersion).IsConcurrencyToken();
            });
            modelBuilder.Entity<DoctorInformation>(doctor =>
            {
                doctor.HasKey(d => d.LoggedInUserId);
                doctor.Property(d => d.LoggedInUserId).ValueGeneratedNever();
                doctor.HasOne<Users>().WithOne().HasForeignKey<DoctorInformation>(d => d.LoggedInUserId).OnDelete(DeleteBehavior.Cascade);
                doctor.Property(d => d.DoctorNameEnglish).IsRequired().HasMaxLength(160);
                doctor.Property(d => d.DoctorNamePashto).IsRequired().HasMaxLength(160);
                doctor.Property(d => d.DoctorProfessionPashto).IsRequired().HasMaxLength(200);
                doctor.Property(d => d.DoctorProfessionEnglish).IsRequired().HasMaxLength(200);
                doctor.Property(d => d.HospitalNamePashto).IsRequired().HasMaxLength(200);
            });
            modelBuilder.Entity<Medication>(medication =>
            {
                medication.Property(m => m.Name).IsRequired().HasMaxLength(200);
                medication.Property(m => m.Type).IsRequired().HasMaxLength(100);
                medication.Property(m => m.Remarks).IsRequired().HasMaxLength(1000);
                medication.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
                medication.HasIndex(m => new { m.UserId, m.Id });
                medication.ToTable(table => table.HasCheckConstraint("CK_Medications_Quantity", "Quantity >= 0"));
            });
            modelBuilder.Entity<Treatment>(treatment =>
            {
                treatment.Property(t => t.RecordedAtUtc).IsRequired();
                treatment.Property(t => t.BloodPressure).IsRequired().HasMaxLength(16);
                treatment.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
                treatment.HasOne(t => t.Patient).WithMany().HasForeignKey(t => t.PatientId).OnDelete(DeleteBehavior.Restrict);
                treatment.HasIndex(t => new { t.UserId, t.RecordedAtUtc });
                treatment.HasIndex(t => new { t.UserId, t.PatientId });
            });
            modelBuilder.Entity<PrescriptionItem>(item =>
            {
                item.Property(i => i.MedicationName).IsRequired().HasMaxLength(200);
                item.Property(i => i.MedicationType).IsRequired().HasMaxLength(100);
                item.Property(i => i.Duration).IsRequired().HasMaxLength(100);
                item.HasOne(i => i.Treatment).WithMany(t => t.PrescriptionItems).HasForeignKey(i => i.TreatmentId).OnDelete(DeleteBehavior.Cascade);
                item.HasOne(i => i.Medication).WithMany().HasForeignKey(i => i.MedicationId).OnDelete(DeleteBehavior.Restrict);
                item.HasIndex(i => new { i.TreatmentId, i.Position }).IsUnique();
                item.ToTable(table => table.HasCheckConstraint("CK_PrescriptionItems_Refills", "Refills BETWEEN 0 AND 100"));
            });
            modelBuilder.Entity<ClinicalNote>(note =>
            {
                note.Property(n => n.Text).IsRequired().HasMaxLength(300);
                note.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
                note.HasIndex(n => new { n.UserId, n.Text }).IsUnique();
            });
            modelBuilder.Entity<TreatmentClinicalNote>(link =>
            {
                link.HasKey(l => new { l.TreatmentId, l.ClinicalNoteId });
                link.HasOne(l => l.Treatment).WithMany(t => t.ClinicalNotes).HasForeignKey(l => l.TreatmentId).OnDelete(DeleteBehavior.Cascade);
                link.HasOne(l => l.ClinicalNote).WithMany().HasForeignKey(l => l.ClinicalNoteId).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
