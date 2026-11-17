using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Meetings (an admin arranges them for several members of staff) and their attendees, and the day a notification is
    /// about (<c>Notifications.CalendarDate</c>), for meeting notifications' links. Adds only: no existing row changes.
    /// </summary>
    public partial class AddedMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CalendarDate",
                table: "Notifications",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OrganiserUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAttendees",
                columns: table => new
                {
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAttendees", x => new { x.MeetingId, x.StaffUserId });
                    table.ForeignKey(
                        name: "FK_MeetingAttendees_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendees_StaffUserId",
                table: "MeetingAttendees",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_StartUtc",
                table: "Meetings",
                column: "StartUtc");
        }

        /// <summary>
        /// Drops meetings, and the notifications about them (kinds 4 to 6), which would link to nothing and have kinds
        /// the code before doesn't know.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Notifications WHERE Kind IN (4, 5, 6);");

            migrationBuilder.DropTable(
                name: "MeetingAttendees");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropColumn(
                name: "CalendarDate",
                table: "Notifications");
        }
    }
}
