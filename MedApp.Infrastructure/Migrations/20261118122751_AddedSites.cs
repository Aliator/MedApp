using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClosedDays_Date",
                table: "ClosedDays");

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "ClosedDays",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressLines = table.Column<string>(type: "nvarchar(302)", maxLength: 302, nullable: true),
                    Postcode = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: true),
                    Phone = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Sites",
                columns: new[] { "Id", "AddressLines", "CreatedAt", "LastUpdated", "Name", "Phone", "Postcode" },
                values: new object[] { new Guid("5173e000-0000-4000-8000-000000000001"), null, new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Main site", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ClosedDays_Date_SiteId",
                table: "ClosedDays",
                columns: new[] { "Date", "SiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClosedDays_SiteId",
                table: "ClosedDays",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_SiteId_Name",
                table: "Rooms",
                columns: new[] { "SiteId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_Name",
                table: "Sites",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClosedDays_Sites_SiteId",
                table: "ClosedDays",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sites' closed days go with the sites: one date could have one everywhere and others at sites, which the
            // old one-per-date index would refuse.
            migrationBuilder.Sql("DELETE FROM ClosedDays WHERE SiteId IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_ClosedDays_Sites_SiteId",
                table: "ClosedDays");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_ClosedDays_Date_SiteId",
                table: "ClosedDays");

            migrationBuilder.DropIndex(
                name: "IX_ClosedDays_SiteId",
                table: "ClosedDays");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "ClosedDays");

            migrationBuilder.CreateIndex(
                name: "IX_ClosedDays_Date",
                table: "ClosedDays",
                column: "Date",
                unique: true);
        }
    }
}
