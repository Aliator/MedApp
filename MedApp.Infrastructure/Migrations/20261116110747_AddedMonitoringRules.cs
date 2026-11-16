using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedMonitoringRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Event = table.Column<int>(type: "int", nullable: false),
                    MinimumPriority = table.Column<int>(type: "int", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StageDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureAssigned = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringRules_Event_IsEnabled",
                table: "MonitoringRules",
                columns: new[] { "Event", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringRules_OwnerUserId",
                table: "MonitoringRules",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringRules");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");
        }
    }
}
