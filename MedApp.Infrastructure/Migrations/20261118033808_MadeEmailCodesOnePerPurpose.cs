using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MadeEmailCodesOnePerPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailCodes_UserId_Purpose",
                table: "EmailCodes");

            // Codes outside sign-in last 10 minutes and can be asked for again, so any there now go, rather than risk two
            // for one account and purpose stopping the unique index.
            migrationBuilder.Sql("DELETE FROM [EmailCodes] WHERE [ChallengeId] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCodes_UserId_Purpose",
                table: "EmailCodes",
                columns: new[] { "UserId", "Purpose" },
                unique: true,
                filter: "[ChallengeId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailCodes_UserId_Purpose",
                table: "EmailCodes");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCodes_UserId_Purpose",
                table: "EmailCodes",
                columns: new[] { "UserId", "Purpose" });
        }
    }
}
