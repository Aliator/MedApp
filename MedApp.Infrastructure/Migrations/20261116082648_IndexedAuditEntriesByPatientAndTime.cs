using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IndexedAuditEntriesByPatientAndTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId",
                table: "PatientProcedureAuditEntries");

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId_AtUtc",
                table: "PatientProcedureAuditEntries",
                columns: new[] { "PatientId", "AtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId_AtUtc",
                table: "PatientProcedureAuditEntries");

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId",
                table: "PatientProcedureAuditEntries",
                column: "PatientId");
        }
    }
}
