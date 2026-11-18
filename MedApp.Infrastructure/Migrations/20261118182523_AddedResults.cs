using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Results sign-off (centre plan decisions 91 to 97): <c>Results</c>, and <c>WaitsForResults</c> on stage definitions
    /// and pathways' stages (false for every existing one, so nothing changes on deploy). Also the new permission
    /// <c>SignOffResults</c> for the starting Clinician and Senior clinician roles (decision 74's table), by their fixed
    /// IDs, so a role an admin made with one of those names is left alone. Admin doesn't get it: it's clinical. The values
    /// are written here, not read from <c>StandardRoles</c>, so the migration never changes.
    /// </summary>
    public partial class AddedResults : Migration
    {
        private const string ClinicianId = "7a3b0000-0000-4000-8000-000000000003";
        private const string SeniorClinicianId = "7a3b0000-0000-4000-8000-000000000008";
        private const string Permission = "SignOffResults";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WaitsForResults",
                table: "PatientPathwayStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WaitsForResults",
                table: "PatientPathwayStageDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SpecimenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReportAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SeenByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignedOffAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedOffByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: true),
                    ActionDetail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EnteredInErrorAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnteredInErrorByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Results_PatientPathways_PathwayId",
                        column: x => x.PathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Results_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Results_Specimens_SpecimenId",
                        column: x => x.SpecimenId,
                        principalTable: "Specimens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "PatientPathwayStageDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "WaitsForResults",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_Results_PathwayId",
                table: "Results",
                column: "PathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_PatientId",
                table: "Results",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_ReportAttachmentId",
                table: "Results",
                column: "ReportAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_SpecimenId",
                table: "Results",
                column: "SpecimenId",
                unique: true,
                filter: "[SpecimenId] IS NOT NULL AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_Results_Status_ResponsibleUserId_ReportedAtUtc",
                table: "Results",
                columns: new[] { "Status", "ResponsibleUserId", "ReportedAtUtc" });

            migrationBuilder.Sql($"""
                INSERT INTO RolePermissions (RoleId, Permission)
                SELECT role.Id, '{Permission}' FROM AspNetRoles AS role
                WHERE role.Id IN ('{ClinicianId}', '{SeniorClinicianId}')
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions AS rp WHERE rp.RoleId = role.Id AND rp.Permission = '{Permission}');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older code doesn't know the permission (a name it doesn't know grants nothing), but take it out tidily.
            migrationBuilder.Sql($"DELETE FROM RolePermissions WHERE Permission = '{Permission}';");

            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropColumn(
                name: "WaitsForResults",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "WaitsForResults",
                table: "PatientPathwayStageDefinitions");
        }
    }
}
