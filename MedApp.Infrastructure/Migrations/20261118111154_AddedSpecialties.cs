using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing template and pathway goes into General (the columns' default, which stays, so an API
            // copy from before specialties can still create both during a rollout). The foreign keys are added once
            // General exists.
            migrationBuilder.AddColumn<Guid>(
                name: "SpecialtyId",
                table: "PatientPathways",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("33333333-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "SpecialtyId",
                table: "PathwayTemplates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("33333333-0000-0000-0000-000000000001"));

            migrationBuilder.CreateTable(
                name: "Specialties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specialties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffSpecialties",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffSpecialties", x => new { x.UserId, x.SpecialtyId });
                    table.ForeignKey(
                        name: "FK_StaffSpecialties_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffSpecialties_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Specialties",
                columns: new[] { "Id", "CreatedAt", "LastUpdated", "Name", "Version" },
                values: new object[] { new Guid("33333333-0000-0000-0000-000000000001"), new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Utc), "General", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPathways_SpecialtyId",
                table: "PatientPathways",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_PathwayTemplates_SpecialtyId",
                table: "PathwayTemplates",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_Specialties_Name",
                table: "Specialties",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffSpecialties_SpecialtyId",
                table: "StaffSpecialties",
                column: "SpecialtyId");

            migrationBuilder.AddForeignKey(
                name: "FK_PathwayTemplates_Specialties_SpecialtyId",
                table: "PathwayTemplates",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathways_Specialties_SpecialtyId",
                table: "PatientPathways",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PathwayTemplates_Specialties_SpecialtyId",
                table: "PathwayTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPathways_Specialties_SpecialtyId",
                table: "PatientPathways");

            migrationBuilder.DropTable(
                name: "StaffSpecialties");

            migrationBuilder.DropTable(
                name: "Specialties");

            migrationBuilder.DropIndex(
                name: "IX_PatientPathways_SpecialtyId",
                table: "PatientPathways");

            migrationBuilder.DropIndex(
                name: "IX_PathwayTemplates_SpecialtyId",
                table: "PathwayTemplates");

            migrationBuilder.DropColumn(
                name: "SpecialtyId",
                table: "PatientPathways");

            migrationBuilder.DropColumn(
                name: "SpecialtyId",
                table: "PathwayTemplates");
        }
    }
}
