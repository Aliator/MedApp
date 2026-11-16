using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedNhsNumberAndSnomedCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NhsNumber",
                table: "Patients",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnomedCode",
                table: "PatientProcedureStages",
                type: "varchar(18)",
                unicode: false,
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnomedTerm",
                table: "PatientProcedureStages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnomedCode",
                table: "PatientProcedureStageDefinitions",
                type: "varchar(18)",
                unicode: false,
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnomedTerm",
                table: "PatientProcedureStageDefinitions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PatientProcedureStageDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "SnomedCode", "SnomedTerm" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_NhsNumber",
                table: "Patients",
                column: "NhsNumber",
                unique: true,
                filter: "[NhsNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_NhsNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "NhsNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "SnomedCode",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "SnomedTerm",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "SnomedCode",
                table: "PatientProcedureStageDefinitions");

            migrationBuilder.DropColumn(
                name: "SnomedTerm",
                table: "PatientProcedureStageDefinitions");
        }
    }
}
