using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoghtiaSystem_JahidKawa.Server.Migrations
{
    public partial class AddPrescriptionPrintPreferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrintOnA5",
                table: "DoctorInformation",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PrintGrayscale",
                table: "DoctorInformation",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PrintOnA5", table: "DoctorInformation");
            migrationBuilder.DropColumn(name: "PrintGrayscale", table: "DoctorInformation");
        }
    }
}
