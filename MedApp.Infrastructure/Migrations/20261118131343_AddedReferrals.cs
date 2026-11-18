using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferralId",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReferralEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReferrerPractice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferrerClinician = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferrerOdsCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    RequestedPriority = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisteredByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RejectionReason = table.Column<int>(type: "int", nullable: true),
                    DecisionDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RedirectedTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referrals_PatientPathways_PathwayId",
                        column: x => x.PathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Referrals_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referrals_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ReferralId",
                table: "Attachments",
                column: "ReferralId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralEvents_PatientId_AtUtc_Id",
                table: "ReferralEvents",
                columns: new[] { "PatientId", "AtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralEvents_ReferralId_AtUtc",
                table: "ReferralEvents",
                columns: new[] { "ReferralId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_PathwayId",
                table: "Referrals",
                column: "PathwayId",
                unique: true,
                filter: "[PathwayId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_PatientId",
                table: "Referrals",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_SpecialtyId",
                table: "Referrals",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_Status_SpecialtyId_ReceivedOn",
                table: "Referrals",
                columns: new[] { "Status", "SpecialtyId", "ReceivedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralEvents");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_ReferralId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ReferralId",
                table: "Attachments");
        }
    }
}
