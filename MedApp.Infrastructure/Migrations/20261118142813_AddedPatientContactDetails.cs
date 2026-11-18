using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedPatientContactDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PracticeId",
                table: "Referrals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLines",
                table: "Patients",
                type: "nvarchar(302)",
                maxLength: 302,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobilePhone",
                table: "Patients",
                type: "varchar(13)",
                unicode: false,
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Postcode",
                table: "Patients",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PracticeId",
                table: "Patients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderChannel",
                table: "Patients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Practices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OdsCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    AddressLines = table.Column<string>(type: "nvarchar(302)", maxLength: 302, nullable: true),
                    Postcode = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: true),
                    Phone = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    SecureEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Practices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_PracticeId",
                table: "Referrals",
                column: "PracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PracticeId",
                table: "Patients",
                column: "PracticeId");

            migrationBuilder.CreateIndex(
                name: "IX_Practices_Name",
                table: "Practices",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Practices_OdsCode",
                table: "Practices",
                column: "OdsCode",
                unique: true,
                filter: "[OdsCode] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Practices_PracticeId",
                table: "Patients",
                column: "PracticeId",
                principalTable: "Practices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_Practices_PracticeId",
                table: "Referrals",
                column: "PracticeId",
                principalTable: "Practices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Practices_PracticeId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_Practices_PracticeId",
                table: "Referrals");

            migrationBuilder.DropTable(
                name: "Practices");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_PracticeId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Patients_PracticeId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PracticeId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "AddressLines",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "MobilePhone",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Postcode",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PracticeId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ReminderChannel",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "AspNetUsers");
        }
    }
}
