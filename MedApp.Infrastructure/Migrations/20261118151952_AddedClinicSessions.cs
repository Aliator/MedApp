using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedClinicSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClinicSessionId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClinicPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Starts = table.Column<TimeOnly>(type: "time", nullable: false),
                    Ends = table.Column<TimeOnly>(type: "time", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    EveryWeeks = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UntilDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicPatterns_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicPatterns_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatternId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicSessions_ClinicPatterns_PatternId",
                        column: x => x.PatternId,
                        principalTable: "ClinicPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicSessions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicSessions_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClinicSessionId_StartUtc_OnCalendar",
                table: "Appointments",
                columns: new[] { "ClinicSessionId", "StartUtc" },
                unique: true,
                filter: "[ClinicSessionId] IS NOT NULL AND [Status] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RoomId_StartUtc",
                table: "Appointments",
                columns: new[] { "RoomId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicPatterns_RoomId",
                table: "ClinicPatterns",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicPatterns_SpecialtyId",
                table: "ClinicPatterns",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicPatterns_StaffUserId",
                table: "ClinicPatterns",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSessions_Date_StartUtc",
                table: "ClinicSessions",
                columns: new[] { "Date", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSessions_PatternId_Date",
                table: "ClinicSessions",
                columns: new[] { "PatternId", "Date" },
                unique: true,
                filter: "[PatternId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSessions_RoomId_StartUtc",
                table: "ClinicSessions",
                columns: new[] { "RoomId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSessions_SpecialtyId",
                table: "ClinicSessions",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSessions_StaffUserId_StartUtc",
                table: "ClinicSessions",
                columns: new[] { "StaffUserId", "StartUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_ClinicSessions_ClinicSessionId",
                table: "Appointments",
                column: "ClinicSessionId",
                principalTable: "ClinicSessions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Rooms_RoomId",
                table: "Appointments",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_ClinicSessions_ClinicSessionId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Rooms_RoomId",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "ClinicSessions");

            migrationBuilder.DropTable(
                name: "ClinicPatterns");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClinicSessionId_StartUtc_OnCalendar",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_RoomId_StartUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClinicSessionId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Appointments");
        }
    }
}
