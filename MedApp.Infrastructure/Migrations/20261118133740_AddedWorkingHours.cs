using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Working hours per site and per person (centre plan decisions 119 to 122): <c>SiteHours</c> and <c>StaffHours</c>,
    /// dated rows per day of the week. Nobody has any yet, so everyone keeps the default day (Monday to Friday, 08:00 to
    /// 18:00) and nothing changes on deploy. Also the new <c>ManageClinics</c> permission, given to the starting Booking
    /// and Manager roles (by their fixed IDs, so a role an admin made with one of those names is left alone); Admin has
    /// it in code. The values are written here, not read from <c>StandardRoles</c>, so the migration never changes.
    /// </summary>
    public partial class AddedWorkingHours : Migration
    {
        private const string Permission = "ManageClinics";
        private const string BookingId = "7a3b0000-0000-4000-8000-000000000005";
        private const string ManagerId = "7a3b0000-0000-4000-8000-000000000007";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Opens = table.Column<TimeOnly>(type: "time", nullable: false),
                    Closes = table.Column<TimeOnly>(type: "time", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteHours_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Starts = table.Column<TimeOnly>(type: "time", nullable: false),
                    Ends = table.Column<TimeOnly>(type: "time", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffHours_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteHours_SiteId_ValidFrom",
                table: "SiteHours",
                columns: new[] { "SiteId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffHours_SiteId",
                table: "StaffHours",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffHours_StaffUserId_ValidFrom",
                table: "StaffHours",
                columns: new[] { "StaffUserId", "ValidFrom" });

            migrationBuilder.Sql($"""
                INSERT INTO RolePermissions (RoleId, Permission)
                SELECT role.Id, '{Permission}' FROM AspNetRoles AS role
                WHERE role.Id IN ('{BookingId}', '{ManagerId}')
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions AS rp WHERE rp.RoleId = role.Id AND rp.Permission = '{Permission}');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older code doesn't know the permission (a name it doesn't know grants nothing), but take it out tidily.
            migrationBuilder.Sql($"DELETE FROM RolePermissions WHERE Permission = '{Permission}';");

            migrationBuilder.DropTable(
                name: "SiteHours");

            migrationBuilder.DropTable(
                name: "StaffHours");
        }
    }
}
