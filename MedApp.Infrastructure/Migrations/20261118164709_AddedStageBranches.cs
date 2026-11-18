using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedStageBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Branch",
                table: "PatientPathwayStages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChoosesBranch",
                table: "PatientPathwayStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOptional",
                table: "PatientPathwayStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkipDetail",
                table: "PatientPathwayStages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkipReason",
                table: "PatientPathwayStages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAtUtc",
                table: "PatientPathwayStages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SkippedByUserId",
                table: "PatientPathwayStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Branch",
                table: "PathwayTemplateMaps",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChoosesBranch",
                table: "PathwayTemplateMaps",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOptional",
                table: "PathwayTemplateMaps",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Branch",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "ChoosesBranch",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "IsOptional",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "SkipDetail",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "SkippedAtUtc",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "SkippedByUserId",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "Branch",
                table: "PathwayTemplateMaps");

            migrationBuilder.DropColumn(
                name: "ChoosesBranch",
                table: "PathwayTemplateMaps");

            migrationBuilder.DropColumn(
                name: "IsOptional",
                table: "PathwayTemplateMaps");
        }
    }
}
