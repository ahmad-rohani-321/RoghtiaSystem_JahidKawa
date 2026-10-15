using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoghtiaSystem_JahidKawa.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionDoseAndStockTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Dose",
                table: "PrescriptionItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "StockDeducted",
                table: "PrescriptionItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrescriptionItems_Dose",
                table: "PrescriptionItems",
                sql: "Dose > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PrescriptionItems_Dose",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "Dose",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "StockDeducted",
                table: "PrescriptionItems");
        }
    }
}
