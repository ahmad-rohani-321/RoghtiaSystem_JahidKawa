using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoghtiaSystem_JahidKawa.Server.Migrations
{
    /// <inheritdoc />
    public partial class SeparateDiagnosticsAndInvestigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiagnosticInvestigations_UserId_Text",
                table: "DiagnosticInvestigations");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "DiagnosticInvestigations",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Diagnostic");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticInvestigations_UserId_Kind_Text",
                table: "DiagnosticInvestigations",
                columns: new[] { "UserId", "Kind", "Text" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiagnosticInvestigations_UserId_Kind_Text",
                table: "DiagnosticInvestigations");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "DiagnosticInvestigations");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticInvestigations_UserId_Text",
                table: "DiagnosticInvestigations",
                columns: new[] { "UserId", "Text" },
                unique: true);
        }
    }
}
