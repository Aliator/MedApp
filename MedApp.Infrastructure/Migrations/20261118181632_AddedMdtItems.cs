using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// The MDT list (decisions 141 to 144 of <c>docs/centre-phases-3-5-plan.md</c>): <c>MdtItems</c> and who was present
    /// (<c>MdtItemAttendees</c>), and meetings marked as a specialty's MDT (<c>Meetings.IsMdt</c>, <c>SpecialtyId</c>).
    /// The new permission <c>RecordMdtOutcomes</c> goes to the starting roles Clinician and Senior clinician (by their
    /// fixed IDs, where they exist and haven't got it), as <c>StandardRoles</c> has it; the values are written here so
    /// the migration never changes after it ships.
    /// </summary>
    public partial class AddedMdtItems : Migration
    {
        private const string Permission = "RecordMdtOutcomes";

        /// <summary>Clinician and Senior clinician.</summary>
        private static readonly string[] RoleIds =
        [
            "7a3b0000-0000-4000-8000-000000000003",
            "7a3b0000-0000-4000-8000-000000000008",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMdt",
                table: "Meetings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SpecialtyId",
                table: "Meetings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MdtItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MeetingCancelledStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ListedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ListedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OutcomeByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutcomeByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OutcomeAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WithdrawnByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MdtItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MdtItems_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MdtItems_PatientPathways_PathwayId",
                        column: x => x.PathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MdtItems_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MdtItems_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MdtItemAttendees",
                columns: table => new
                {
                    MdtItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MdtItemAttendees", x => new { x.MdtItemId, x.StaffUserId });
                    table.ForeignKey(
                        name: "FK_MdtItemAttendees_MdtItems_MdtItemId",
                        column: x => x.MdtItemId,
                        principalTable: "MdtItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_SpecialtyId",
                table: "Meetings",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_MdtItems_MeetingId",
                table: "MdtItems",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MdtItems_PathwayId",
                table: "MdtItems",
                column: "PathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_MdtItems_PatientId",
                table: "MdtItems",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_MdtItems_SpecialtyId_Status",
                table: "MdtItems",
                columns: new[] { "SpecialtyId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Specialties_SpecialtyId",
                table: "Meetings",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            var roles = string.Join(", ", RoleIds.Select(id => $"('{id}')"));
            migrationBuilder.Sql($"""
                INSERT INTO RolePermissions (RoleId, Permission)
                SELECT role.Id, '{Permission}'
                FROM (VALUES {roles}) AS wanted(Id)
                JOIN AspNetRoles AS role ON role.Id = wanted.Id
                WHERE NOT EXISTS (SELECT 1 FROM RolePermissions AS rp WHERE rp.RoleId = role.Id AND rp.Permission = '{Permission}');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older code knows no such permission; a stored name it doesn't know grants nothing, but it's tidier gone.
            migrationBuilder.Sql($"DELETE FROM RolePermissions WHERE Permission = '{Permission}';");

            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Specialties_SpecialtyId",
                table: "Meetings");

            migrationBuilder.DropTable(
                name: "MdtItemAttendees");

            migrationBuilder.DropTable(
                name: "MdtItems");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_SpecialtyId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "IsMdt",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "SpecialtyId",
                table: "Meetings");
        }
    }
}
