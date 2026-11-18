using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Closed days (bank holidays and other days the clinic is closed), and appointments recorded after the event
    /// (walk-ins). Adds only: a table, and a column every existing appointment has as false.
    /// </summary>
    public partial class AddedClosedDaysAndPastAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecordedAfterEvent",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ClosedDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosedDays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosedDays_Date",
                table: "ClosedDays",
                column: "Date",
                unique: true);
        }

        /// <summary>
        /// Back to before closed days and walk-ins. The code before doesn't know the new status (5, entered in error),
        /// history actions (7, recorded after the event; 8, entered in error) or notification kinds (8, day closed; 9,
        /// appointment recorded; 22, stage visit withdrawn): history entries of appointments recorded after the event become the Attended (3) or
        /// Did not attend (4) entries they amount to (their details still say what happened); an appointment withdrawn
        /// as entered in error becomes Cancelled (4), keeping its reason, and its history entry a cancellation (5); and
        /// the notifications are deleted, as <c>AddedMeetings</c>' Down deletes meeting notifications. The closed days go.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE AppointmentEvents SET Action = CASE WHEN JSON_VALUE(Details, '$.outcome') = 'DidNotAttend' THEN 4 ELSE 3 END WHERE Action = 7;");
            migrationBuilder.Sql("UPDATE AppointmentEvents SET Action = 5 WHERE Action = 8;");
            migrationBuilder.Sql("UPDATE Appointments SET Status = 4 WHERE Status = 5;");
            migrationBuilder.Sql("DELETE FROM Notifications WHERE Kind IN (8, 9, 22);");

            migrationBuilder.DropTable(
                name: "ClosedDays");

            migrationBuilder.DropColumn(
                name: "IsRecordedAfterEvent",
                table: "Appointments");
        }
    }
}
