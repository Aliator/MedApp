using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Specimen tracking (centre plan decisions 81 to 90): <c>SpecimenTypes</c> and <c>Laboratories</c> (setup lists), and
    /// <c>Specimens</c> with their numbers from the new sequence <c>SpecimenNumbers</c>. <c>Notifications.SubjectId</c>
    /// (decision 72) came with <c>AddedLetters</c>. Nothing exists yet, so nothing changes on deploy. Also the new
    /// permissions: <c>RecordSpecimens</c> for the starting Clinician, Senior clinician, Nurse and Lab roles, and
    /// <c>ProcessSpecimens</c> for Lab (decision 74's table), by their fixed IDs, so a role an admin made with one of those
    /// names is left alone. Admin doesn't get them: they're clinical. The values are written here, not read from
    /// <c>StandardRoles</c>, so the migration never changes.
    /// </summary>
    public partial class AddedSpecimens : Migration
    {
        private const string ClinicianId = "7a3b0000-0000-4000-8000-000000000003";
        private const string NurseId = "7a3b0000-0000-4000-8000-000000000004";
        private const string LabId = "7a3b0000-0000-4000-8000-000000000006";
        private const string SeniorClinicianId = "7a3b0000-0000-4000-8000-000000000008";

        private static readonly (string Permission, string[] RoleIds)[] Grants =
        [
            ("RecordSpecimens", [ClinicianId, SeniorClinicianId, NurseId, LabId]),
            ("ProcessSpecimens", [LabId]),
        ];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "SpecimenNumbers");

            migrationBuilder.CreateTable(
                name: "Laboratories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UsesMedApp = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laboratories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecimenTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpectedTurnaroundDays = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecimenTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Specimens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecimenTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LaboratoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestingUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TakenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TakenByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CourierReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LabReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectionReason = table.Column<int>(type: "int", nullable: true),
                    ClosedDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specimens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Specimens_Laboratories_LaboratoryId",
                        column: x => x.LaboratoryId,
                        principalTable: "Laboratories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Specimens_PatientPathways_PathwayId",
                        column: x => x.PathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Specimens_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Specimens_SpecimenTypes_SpecimenTypeId",
                        column: x => x.SpecimenTypeId,
                        principalTable: "SpecimenTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Laboratories_Name",
                table: "Laboratories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_LaboratoryId",
                table: "Specimens",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_Number",
                table: "Specimens",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_PathwayId",
                table: "Specimens",
                column: "PathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_PatientId",
                table: "Specimens",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_ReportAttachmentId",
                table: "Specimens",
                column: "ReportAttachmentId",
                filter: "[ReportAttachmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_SpecimenTypeId",
                table: "Specimens",
                column: "SpecimenTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_Status_LaboratoryId_SentAtUtc",
                table: "Specimens",
                columns: new[] { "Status", "LaboratoryId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecimenTypes_Name",
                table: "SpecimenTypes",
                column: "Name",
                unique: true);

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
            // Older code doesn't know the permissions (a name it doesn't know grants nothing), but take them out tidily.
            foreach (var (permission, _) in Grants)
                migrationBuilder.Sql($"DELETE FROM RolePermissions WHERE Permission = '{permission}';");

            migrationBuilder.DropTable(
                name: "Specimens");

            migrationBuilder.DropTable(
                name: "Laboratories");

            migrationBuilder.DropTable(
                name: "SpecimenTypes");

            migrationBuilder.DropSequence(
                name: "SpecimenNumbers");
        }
    }
}
