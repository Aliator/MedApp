using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedActivityLogAndPatientPrivacyFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnonymisedAtUtc",
                table: "Patients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeReason",
                table: "Patients",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputedAtUtc",
                table: "Patients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RestrictedAtUtc",
                table: "Patients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionReason",
                table: "Patients",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionKeptReason",
                table: "Patients",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RetentionKeptUntil",
                table: "Patients",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcedureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEntries_AtUtc",
                table: "ActivityEntries",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEntries_PatientId_AtUtc",
                table: "ActivityEntries",
                columns: new[] { "PatientId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEntries_UserId_Action_PatientId_AtUtc",
                table: "ActivityEntries",
                columns: new[] { "UserId", "Action", "PatientId", "AtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEntries");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "DisputeReason",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "DisputedAtUtc",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "RestrictedAtUtc",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "RestrictionReason",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "RetentionKeptReason",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "RetentionKeptUntil",
                table: "Patients");
        }
    }
}
