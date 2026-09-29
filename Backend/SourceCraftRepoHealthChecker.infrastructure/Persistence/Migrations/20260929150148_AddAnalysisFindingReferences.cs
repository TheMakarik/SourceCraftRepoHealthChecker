using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SourceCraftRepoHealthChecker.infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisFindingReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                table: "AnalysisFindings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "AnalysisFindings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileLine",
                table: "AnalysisFindings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommitSha",
                table: "AnalysisFindings");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "AnalysisFindings");

            migrationBuilder.DropColumn(
                name: "FileLine",
                table: "AnalysisFindings");
        }
    }
}
