using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Letters (centre plan decisions 100 to 108): <c>Letters</c> and <c>LetterRecipients</c>, the stage option
    /// <c>NeedsLetterKind</c> on stage definitions and pathways' stages (null: none, so every stage behaves as before),
    /// and <c>Notifications.SubjectId</c> (decision 72). Nothing exists yet, so nothing changes on deploy. Also the new
    /// permissions, by the starting roles' fixed IDs (decision 74's table): <c>WriteLetters</c>, <c>ApproveLetters</c>
    /// and <c>SendLetters</c> for Clinician; <c>WriteLetters</c> and <c>SendLetters</c> for Nurse; <c>SendLetters</c> for
    /// Booking. Admin doesn't get them: they're clinical. A role an admin made with one of those names is left alone. The
    /// values are written here, not read from <c>StandardRoles</c>, so the migration never changes.
    /// </summary>
    public partial class AddedLetters : Migration
    {
        private const string ClinicianId = "7a3b0000-0000-4000-8000-000000000003";
        private const string NurseId = "7a3b0000-0000-4000-8000-000000000004";
        private const string BookingId = "7a3b0000-0000-4000-8000-000000000005";

        private static readonly (string Permission, string[] RoleIds)[] Grants =
        [
            ("WriteLetters", [ClinicianId, NurseId]),
            ("ApproveLetters", [ClinicianId]),
            ("SendLetters", [ClinicianId, NurseId, BookingId]),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NeedsLetterKind",
                table: "PatientPathwayStages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeedsLetterKind",
                table: "PatientPathwayStageDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Letters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferralId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Letterhead = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Closing = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LargePrint = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PdfAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WithdrawnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplacesLetterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Letters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Letters_LetterTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "LetterTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Letters_PatientPathways_PathwayId",
                        column: x => x.PathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Letters_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Letters_Referrals_ReferralId",
                        column: x => x.ReferralId,
                        principalTable: "Referrals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LetterRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PracticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLines = table.Column<string>(type: "nvarchar(302)", maxLength: 302, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    DispatchStatus = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: true),
                    SentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransportReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetterRecipients_Letters_LetterId",
                        column: x => x.LetterId,
                        principalTable: "Letters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "PatientPathwayStageDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "NeedsLetterKind",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_LetterRecipients_LetterId_DispatchStatus",
                table: "LetterRecipients",
                columns: new[] { "LetterId", "DispatchStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Letters_PathwayId",
                table: "Letters",
                column: "PathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_Letters_PatientId",
                table: "Letters",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Letters_ReferralId",
                table: "Letters",
                column: "ReferralId");

            migrationBuilder.CreateIndex(
                name: "IX_Letters_Status_ApproverUserId",
                table: "Letters",
                columns: new[] { "Status", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Letters_Status_AuthorUserId",
                table: "Letters",
                columns: new[] { "Status", "AuthorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Letters_TemplateId",
                table: "Letters",
                column: "TemplateId");

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
                name: "LetterRecipients");

            migrationBuilder.DropTable(
                name: "Letters");

            migrationBuilder.DropColumn(
                name: "NeedsLetterKind",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "NeedsLetterKind",
                table: "PatientPathwayStageDefinitions");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Notifications");
        }
    }
}
