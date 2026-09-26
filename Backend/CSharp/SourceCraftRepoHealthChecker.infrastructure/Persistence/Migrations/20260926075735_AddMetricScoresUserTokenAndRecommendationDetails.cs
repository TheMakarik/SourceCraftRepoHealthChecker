using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SourceCraftRepoHealthChecker.infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricScoresUserTokenAndRecommendationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceCraftToken",
                table: "Users",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Repositories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Evidence",
                table: "Recommendations",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WhyImportant",
                table: "Recommendations",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "MetricScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<int>(type: "integer", nullable: false),
                    RawValue = table.Column<double>(type: "double precision", nullable: false),
                    NormalizedScore = table.Column<double>(type: "double precision", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    DataStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricScores_AnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "AnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetricScores_AnalysisRunId",
                table: "MetricScores",
                column: "AnalysisRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetricScores");

            migrationBuilder.DropColumn(
                name: "SourceCraftToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "Evidence",
                table: "Recommendations");

            migrationBuilder.DropColumn(
                name: "WhyImportant",
                table: "Recommendations");
        }
    }
}
