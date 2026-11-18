using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Consent records (centre plan decisions 145 to 147): <c>ConsentForms</c> (a setup list) and <c>ConsentRecords</c>,
    /// and <c>NeedsConsent</c> on stage definitions and pathway stages (false for every existing one, so nothing changes
    /// on deploy). Also the new permission <c>RecordConsent</c> for the starting Clinician, Senior clinician and Nurse
    /// roles (decision 74's table), by their fixed IDs, so a role an admin made with one of those names is left alone.
    /// Admin doesn't get it: it's clinical. The values are written here, not read from <c>StandardRoles</c>, so the
    /// migration never changes. Down drops the new columns and tables (the scaffolder's data-loss warning): rolling back
    /// loses the consent records, which older code can't read.
    /// </summary>
    public partial class AddedConsentRecords : Migration
    {
        private const string ClinicianId = "7a3b0000-0000-4000-8000-000000000003";
        private const string NurseId = "7a3b0000-0000-4000-8000-000000000004";
        private const string SeniorClinicianId = "7a3b0000-0000-4000-8000-000000000008";

        private static readonly (string Permission, string[] RoleIds)[] Grants =
        [
            ("RecordConsent", [ClinicianId, SeniorClinicianId, NurseId]),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsConsent",
                table: "PatientPathwayStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsConsent",
                table: "PatientPathwayStageDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConsentForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FormVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsentFormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FormVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SignedBy = table.Column<int>(type: "int", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScanAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WithdrawnOn = table.Column<DateOnly>(type: "date", nullable: true),
                    StatusDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StatusChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_ConsentForms_ConsentFormId",
                        column: x => x.ConsentFormId,
                        principalTable: "ConsentForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_PatientPathways_PathwayId",
                        column: x => x.PathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "PatientPathwayStageDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "NeedsConsent",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentForms_Name_FormVersion",
                table: "ConsentForms",
                columns: new[] { "Name", "FormVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_ConsentFormId",
                table: "ConsentRecords",
                column: "ConsentFormId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_PathwayId",
                table: "ConsentRecords",
                column: "PathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_PatientId",
                table: "ConsentRecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_ScanAttachmentId",
                table: "ConsentRecords",
                column: "ScanAttachmentId",
                filter: "[ScanAttachmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_StageId",
                table: "ConsentRecords",
                column: "StageId");

            foreach (var (permission, roleIds) in Grants)
                migrationBuilder.Sql($"""
                    INSERT INTO RolePermissions (RoleId, Permission)
                    SELECT role.Id, '{permission}' FROM AspNetRoles AS role
                    WHERE role.Id IN ({string.Join(", ", roleIds.Select(id => $"'{id}'"))})
                      AND NOT EXISTS (SELECT 1 FROM RolePermissions AS rp WHERE rp.RoleId = role.Id AND rp.Permission = '{permission}');
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older code doesn't know the permission (a name it doesn't know grants nothing), but take it out tidily.
            foreach (var (permission, _) in Grants)
                migrationBuilder.Sql($"DELETE FROM RolePermissions WHERE Permission = '{permission}';");

            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "ConsentForms");

            migrationBuilder.DropColumn(
                name: "NeedsConsent",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "NeedsConsent",
                table: "PatientPathwayStageDefinitions");
        }
    }
}
