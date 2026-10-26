using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoghtiaSystem_JahidKawa.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentDiagnosticsInvestigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiagnosticInvestigations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticInvestigations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticInvestigations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentDiagnosticInvestigations",
                columns: table => new
                {
                    TreatmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiagnosticInvestigationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentDiagnosticInvestigations", x => new { x.TreatmentId, x.DiagnosticInvestigationId });
                    table.ForeignKey(
                        name: "FK_TreatmentDiagnosticInvestigations_DiagnosticInvestigations_DiagnosticInvestigationId",
                        column: x => x.DiagnosticInvestigationId,
                        principalTable: "DiagnosticInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentDiagnosticInvestigations_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticInvestigations_UserId_Text",
                table: "DiagnosticInvestigations",
                columns: new[] { "UserId", "Text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentDiagnosticInvestigations_DiagnosticInvestigationId",
                table: "TreatmentDiagnosticInvestigations",
                column: "DiagnosticInvestigationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TreatmentDiagnosticInvestigations");

            migrationBuilder.DropTable(
                name: "DiagnosticInvestigations");
        }
    }
}
