using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedMeetingReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RepliedAtUtc",
                table: "MeetingAttendees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Reply",
                table: "MeetingAttendees",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepliedAtUtc",
                table: "MeetingAttendees");

            migrationBuilder.DropColumn(
                name: "Reply",
                table: "MeetingAttendees");
        }
    }
}
