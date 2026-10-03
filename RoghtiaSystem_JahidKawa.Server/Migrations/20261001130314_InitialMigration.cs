using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoghtiaSystem_JahidKawa.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
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
                name: "Medications");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
