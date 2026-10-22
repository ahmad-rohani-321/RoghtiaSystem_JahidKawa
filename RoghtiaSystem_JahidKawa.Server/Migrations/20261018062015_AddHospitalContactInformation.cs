using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoghtiaSystem_JahidKawa.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalContactInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HospitalAddress",
                table: "DoctorInformation",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HospitalPhoneNumbers",
                table: "DoctorInformation",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HospitalAddress",
                table: "DoctorInformation");

            migrationBuilder.DropColumn(
                name: "HospitalPhoneNumbers",
                table: "DoctorInformation");
        }
    }
}
