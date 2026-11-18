using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedServiceAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SpecialtyId",
                table: "MonitoringRules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThresholdDays",
                table: "MonitoringRules",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MonitoringAlertsSent",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringAlertsSent", x => new { x.RuleId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_MonitoringAlertsSent_MonitoringRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "MonitoringRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The service alert rules (events 8 to 13) mean nothing without their days, so they go with them.
            migrationBuilder.Sql("DELETE FROM MonitoringRules WHERE Event BETWEEN 8 AND 13;");

            migrationBuilder.DropTable(
                name: "MonitoringAlertsSent");

            migrationBuilder.DropColumn(
                name: "SpecialtyId",
                table: "MonitoringRules");

            migrationBuilder.DropColumn(
                name: "ThresholdDays",
                table: "MonitoringRules");
        }
    }
}
