using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// The waiting-time clock: targets on specialties (the owner's defaults, 14, 28, 126 and 126 days for Urgent, High,
    /// Medium and Low, for every existing specialty), the clock on referrals, their pauses, and the clock-stop stage on
    /// template maps and pathway stages. Existing referrals get their target from today's specialty table (for the
    /// priority asked for, or, once accepted, the pathway's priority the triager chose), counting from the day they were
    /// received; those closed without a pathway are stopped on the day they were decided, and
    /// accepted ones whose pathway has closed on the day it last changed. An accepted pathway still open keeps the
    /// target date it has until its clock next moves it.
    /// </summary>
    public partial class AddedWaitingTimeClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Referrals_Status_SpecialtyId_ReceivedOn",
                table: "Referrals");

            migrationBuilder.AddColumn<int>(
                name: "TargetDaysHigh",
                table: "Specialties",
                type: "int",
                nullable: false,
                defaultValue: 28);

            migrationBuilder.AddColumn<int>(
                name: "TargetDaysLow",
                table: "Specialties",
                type: "int",
                nullable: false,
                defaultValue: 126);

            migrationBuilder.AddColumn<int>(
                name: "TargetDaysMedium",
                table: "Specialties",
                type: "int",
                nullable: false,
                defaultValue: 126);

            migrationBuilder.AddColumn<int>(
                name: "TargetDaysUrgent",
                table: "Specialties",
                type: "int",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PauseReviewOn",
                table: "Referrals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PausedDays",
                table: "Referrals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PausedSinceOn",
                table: "Referrals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StopReason",
                table: "Referrals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StoppedOn",
                table: "Referrals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetDays",
                table: "Referrals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TargetOn",
                table: "Referrals",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "WaitStartOn",
                table: "Referrals",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<bool>(
                name: "StopsClock",
                table: "PatientPathwayStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StopsClock",
                table: "PathwayTemplateMaps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing referrals: the clock their specialty's target for the priority asked for, from the day received.
            migrationBuilder.Sql("""
                UPDATE r SET TargetDays = CASE r.RequestedPriority
                        WHEN 3 THEN s.TargetDaysUrgent
                        WHEN 2 THEN s.TargetDaysHigh
                        WHEN 1 THEN s.TargetDaysMedium
                        ELSE s.TargetDaysLow END
                FROM Referrals r
                JOIN Specialties s ON s.Id = r.SpecialtyId;

                -- Accepted into a pathway: the clock follows the priority the triager chose (the pathway's), as
                -- accepting does now, not the one asked for (Urgent 3, High 2, Medium 1, Low 0).
                UPDATE r SET TargetDays = CASE p.Priority
                        WHEN 3 THEN s.TargetDaysUrgent
                        WHEN 2 THEN s.TargetDaysHigh
                        WHEN 1 THEN s.TargetDaysMedium
                        ELSE s.TargetDaysLow END
                FROM Referrals r
                JOIN PatientPathways p ON p.Id = r.PathwayId
                JOIN Specialties s ON s.Id = r.SpecialtyId
                WHERE r.Status = 1;

                UPDATE Referrals SET WaitStartOn = ReceivedOn, TargetOn = DATEADD(day, TargetDays, ReceivedOn);

                -- Closed without a pathway: stopped on the day it was decided (UK), for the matching reason
                -- (Rejected 2 -> 3, RedirectedElsewhere 3 -> 4, Withdrawn 4 -> 5, EnteredInError 5 -> 6).
                UPDATE Referrals
                SET StoppedOn = CAST((DecidedAtUtc AT TIME ZONE 'UTC') AT TIME ZONE 'GMT Standard Time' AS date),
                    StopReason = Status + 1
                WHERE Status IN (2, 3, 4, 5) AND DecidedAtUtc IS NOT NULL;

                -- Accepted, with the pathway closed since (Completed 5, Cancelled 6, Archived 8): stopped on the day it
                -- last changed, as discharged or cancelled (PathwayDischarged 1, PathwayCancelled 2).
                UPDATE r
                SET StoppedOn = CAST((p.LastUpdated AT TIME ZONE 'UTC') AT TIME ZONE 'GMT Standard Time' AS date),
                    StopReason = CASE p.Status WHEN 6 THEN 2 ELSE 1 END
                FROM Referrals r
                JOIN PatientPathways p ON p.Id = r.PathwayId
                WHERE r.Status = 1 AND p.Status IN (5, 6, 8);

                -- Accepted, with its pathway since deleted (only a cancelled or archived one can be): stopped when decided.
                UPDATE Referrals
                SET StoppedOn = CAST((DecidedAtUtc AT TIME ZONE 'UTC') AT TIME ZONE 'GMT Standard Time' AS date),
                    StopReason = 2
                WHERE Status = 1 AND PathwayId IS NULL AND StoppedOn IS NULL AND DecidedAtUtc IS NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "ReferralClockPauses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PausedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PausedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PausedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PausedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ResumedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ResumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResumedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResumedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralClockPauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralClockPauses_Referrals_ReferralId",
                        column: x => x.ReferralId,
                        principalTable: "Referrals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Specialties",
                keyColumn: "Id",
                keyValue: new Guid("33333333-0000-0000-0000-000000000001"),
                columns: new[] { "TargetDaysHigh", "TargetDaysLow", "TargetDaysMedium", "TargetDaysUrgent" },
                values: new object[] { 28, 126, 126, 14 });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_PauseReviewOn",
                table: "Referrals",
                column: "PauseReviewOn",
                filter: "[PauseReviewOn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_Status_SpecialtyId_WaitStartOn",
                table: "Referrals",
                columns: new[] { "Status", "SpecialtyId", "WaitStartOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralClockPauses_PatientId",
                table: "ReferralClockPauses",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralClockPauses_ReferralId_Open",
                table: "ReferralClockPauses",
                column: "ReferralId",
                unique: true,
                filter: "[ResumedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralClockPauses");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_PauseReviewOn",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_Status_SpecialtyId_WaitStartOn",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "TargetDaysHigh",
                table: "Specialties");

            migrationBuilder.DropColumn(
                name: "TargetDaysLow",
                table: "Specialties");

            migrationBuilder.DropColumn(
                name: "TargetDaysMedium",
                table: "Specialties");

            migrationBuilder.DropColumn(
                name: "TargetDaysUrgent",
                table: "Specialties");

            migrationBuilder.DropColumn(
                name: "PauseReviewOn",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "PausedDays",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "PausedSinceOn",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "StopReason",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "StoppedOn",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "TargetDays",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "TargetOn",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "WaitStartOn",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "StopsClock",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "StopsClock",
                table: "PathwayTemplateMaps");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_Status_SpecialtyId_ReceivedOn",
                table: "Referrals",
                columns: new[] { "Status", "SpecialtyId", "ReceivedOn" });
        }
    }
}
