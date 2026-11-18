using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedScheduledJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                columns: table => new
                {
                    Name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    NextRunUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseHolder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutcome = table.Column<int>(type: "int", nullable: true),
                    LastCount = table.Column<int>(type: "int", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    LastErrorType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobs", x => x.Name);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAtUtc",
                table: "Notifications",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledJobs");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CreatedAtUtc",
                table: "Notifications");
        }
    }
}
