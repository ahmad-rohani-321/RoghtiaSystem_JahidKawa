using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoghtiaSystem_JahidKawa.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddReusableAdditionalInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_AdditionalInstructions_UserId_Text",
                table: "AdditionalInstructions",
                columns: new[] { "UserId", "Text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentAdditionalInstructions_AdditionalInstructionId",
                table: "TreatmentAdditionalInstructions",
                column: "AdditionalInstructionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TreatmentAdditionalInstructions");

            migrationBuilder.DropTable(
                name: "AdditionalInstructions");
        }
    }
}
