using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SourceCraftRepoHealthChecker.infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNullableScoreAndNumericExpectedImpact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Recommendations\" ALTER COLUMN \"ExpectedImpact\" DROP NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE \"Recommendations\" ALTER COLUMN \"ExpectedImpact\" TYPE integer USING CASE WHEN \"ExpectedImpact\" ~ '^-?[0-9]+$' THEN \"ExpectedImpact\"::integer ELSE NULL END;");

            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "AnalysisRuns",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "AnalysisRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.Sql("ALTER TABLE \"Recommendations\" ALTER COLUMN \"ExpectedImpact\" TYPE character varying(1024) USING \"ExpectedImpact\"::text;");
            migrationBuilder.Sql("UPDATE \"Recommendations\" SET \"ExpectedImpact\" = '' WHERE \"ExpectedImpact\" IS NULL;");
            migrationBuilder.Sql("ALTER TABLE \"Recommendations\" ALTER COLUMN \"ExpectedImpact\" SET NOT NULL;");
        }
    }
}
