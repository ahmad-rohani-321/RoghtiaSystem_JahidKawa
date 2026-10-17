using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoghtiaSystem_JahidKawa.Server.Migrations
{
    /// <inheritdoc />
    public partial class FirstMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "BLOB", nullable: true),
                    PasswordSalt = table.Column<byte[]>(type: "BLOB", nullable: true),
                    PasswordVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    PINCode = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false, defaultValue: ""),
                    PINFailedAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    PINLockedUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "User")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdditionalInstructions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdditionalInstructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdditionalInstructions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalNotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorInformation",
                columns: table => new
                {
                    LoggedInUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DoctorNameEnglish = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DoctorNamePashto = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DoctorProfessionPashto = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DoctorProfessionEnglish = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DoctorPhoto = table.Column<byte[]>(type: "BLOB", nullable: true),
                    DoctorLogo = table.Column<byte[]>(type: "BLOB", nullable: true),
                    HospitalLogo = table.Column<byte[]>(type: "BLOB", nullable: true),
                    HospitalNamePashto = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorInformation", x => x.LoggedInUserId);
                    table.ForeignKey(
                        name: "FK_DoctorInformation_Users_LoggedInUserId",
                        column: x => x.LoggedInUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Medications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medications", x => x.Id);
                    table.CheckConstraint("CK_Medications_Quantity", "Quantity >= 0");
                    table.ForeignKey(
                        name: "FK_Medications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Age = table.Column<int>(type: "INTEGER", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.CheckConstraint("CK_Patients_Age", "Age BETWEEN 0 AND 150");
                    table.ForeignKey(
                        name: "FK_Patients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Treatments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PatientId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BloodPressure = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AdditionalInstructions = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treatments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Treatments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Treatments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TreatmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    MedicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    MedicationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MedicationType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Dose = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    StockDeducted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, defaultValue: "Once daily"),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItems", x => x.Id);
                    table.CheckConstraint("CK_PrescriptionItems_Dose", "Dose > 0");
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_Medications_MedicationId",
                        column: x => x.MedicationId,
                        principalTable: "Medications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentAdditionalInstructions",
                columns: table => new
                {
                    TreatmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    AdditionalInstructionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentAdditionalInstructions", x => new { x.TreatmentId, x.AdditionalInstructionId });
                    table.ForeignKey(
                        name: "FK_TreatmentAdditionalInstructions_AdditionalInstructions_AdditionalInstructionId",
                        column: x => x.AdditionalInstructionId,
                        principalTable: "AdditionalInstructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentAdditionalInstructions_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentClinicalNotes",
                columns: table => new
                {
                    TreatmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClinicalNoteId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentClinicalNotes", x => new { x.TreatmentId, x.ClinicalNoteId });
                    table.ForeignKey(
                        name: "FK_TreatmentClinicalNotes_ClinicalNotes_ClinicalNoteId",
                        column: x => x.ClinicalNoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentClinicalNotes_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdditionalInstructions_UserId_Text",
                table: "AdditionalInstructions",
                columns: new[] { "UserId", "Text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_UserId_Text",
                table: "ClinicalNotes",
                columns: new[] { "UserId", "Text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medications_UserId_Id",
                table: "Medications",
                columns: new[] { "UserId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserId_Code",
                table: "Patients",
                columns: new[] { "UserId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserId_Id",
                table: "Patients",
                columns: new[] { "UserId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_MedicationId",
                table: "PrescriptionItems",
                column: "MedicationId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_TreatmentId_Position",
                table: "PrescriptionItems",
                columns: new[] { "TreatmentId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentAdditionalInstructions_AdditionalInstructionId",
                table: "TreatmentAdditionalInstructions",
                column: "AdditionalInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentClinicalNotes_ClinicalNoteId",
                table: "TreatmentClinicalNotes",
                column: "ClinicalNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_PatientId",
                table: "Treatments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_UserId_PatientId",
                table: "Treatments",
                columns: new[] { "UserId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_UserId_RecordedAtUtc",
                table: "Treatments",
                columns: new[] { "UserId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUserName",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorInformation");

            migrationBuilder.DropTable(
                name: "PrescriptionItems");

            migrationBuilder.DropTable(
                name: "TreatmentAdditionalInstructions");

            migrationBuilder.DropTable(
                name: "TreatmentClinicalNotes");

            migrationBuilder.DropTable(
                name: "Medications");

            migrationBuilder.DropTable(
                name: "AdditionalInstructions");

            migrationBuilder.DropTable(
                name: "ClinicalNotes");

            migrationBuilder.DropTable(
                name: "Treatments");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
