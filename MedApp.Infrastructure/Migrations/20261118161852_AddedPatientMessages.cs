using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedPatientMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DnaFollowUp",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DnaFollowUpAtUtc",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DnaFollowUpByUserId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DnaFollowUpDetail",
                table: "Appointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PatientMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false, defaultValue: ""),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    AppointmentStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SendAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientMessages_AppointmentId_Kind_Channel_AppointmentStartUtc",
                table: "PatientMessages",
                columns: new[] { "AppointmentId", "Kind", "Channel", "AppointmentStartUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientMessages_PatientId",
                table: "PatientMessages",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMessages_Status_CreatedAtUtc",
                table: "PatientMessages",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientMessages");

            migrationBuilder.DropColumn(
                name: "DnaFollowUp",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DnaFollowUpAtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DnaFollowUpByUserId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DnaFollowUpDetail",
                table: "Appointments");
        }
    }
}
