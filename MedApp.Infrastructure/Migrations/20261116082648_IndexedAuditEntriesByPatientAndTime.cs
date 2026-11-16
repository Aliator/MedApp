using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Replaces the audit table's index on PatientId with one on (PatientId, AtUtc), for a patient's timeline. The new
    /// index is built before the old one is dropped (and the reverse going down), so reads by patient always have an
    /// index; dropping first would lock the table for the whole build. On a large table, build it online instead
    /// (<c>IsCreatedOnline()</c>), which needs an edition that supports it (Azure SQL, Developer, Enterprise).
    /// </summary>
    public partial class IndexedAuditEntriesByPatientAndTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId_AtUtc",
                table: "PatientProcedureAuditEntries",
                columns: new[] { "PatientId", "AtUtc" });

            migrationBuilder.DropIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId",
                table: "PatientProcedureAuditEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId",
                table: "PatientProcedureAuditEntries",
                column: "PatientId");

            migrationBuilder.DropIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId_AtUtc",
                table: "PatientProcedureAuditEntries");
        }
    }
}
