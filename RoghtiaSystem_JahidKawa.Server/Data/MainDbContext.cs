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
        }
    }
}
