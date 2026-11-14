using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedProcedureWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PatientProcedureStageTemplateMaps",
                table: "PatientProcedureStageTemplateMaps");

            migrationBuilder.DropIndex(
                name: "IX_PatientProcedureStageTemplateMaps_TemplateId_StageOrder",
                table: "PatientProcedureStageTemplateMaps");

            migrationBuilder.DropIndex(
                name: "IX_PatientProcedureStages_PatientProcedureId_StageDefinitionId",
                table: "PatientProcedureStages");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "PatientProcedureStageTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PatientProcedureStages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "PatientProcedureStages",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "PatientProcedureStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "PatientProcedureStages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PatientProcedureStages",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "PatientProcedureStages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StartedByUserId",
                table: "PatientProcedureStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "PatientProcedureStageDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BlockedFromStatus",
                table: "PatientProcedures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "PatientProcedures",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "PatientProcedures",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            // NotStarted (2) was dropped: those procedures become Assigned (1), or Unassigned (0) if nobody is.
            migrationBuilder.Sql("""
                UPDATE p
                SET Status = CASE WHEN EXISTS (
                        SELECT 1 FROM PatientProcedureAssignments a WHERE a.PatientProcedureId = p.Id)
                    THEN 1 ELSE 0 END
                FROM PatientProcedures p
                WHERE p.Status = 2;
                """);

            // Stages now keep a copy of their definition, so later edits to the definition don't rewrite history.
            migrationBuilder.Sql("""
                UPDATE s
                SET Name = d.Name, Description = d.Description, Instructions = d.Instructions
                FROM PatientProcedureStages s
                JOIN PatientProcedureStageDefinitions d ON d.Id = s.StageDefinitionId;
                """);

            // Finished stages are locked, and were started when they were finished (the best record there is).
            migrationBuilder.Sql("""
                UPDATE PatientProcedureStages
                SET IsLocked = 1, StartedAtUtc = CompletedAtUtc, StartedByUserId = CompletedByUserId
                WHERE IsCompleted = 1;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PatientProcedureStageTemplateMaps",
                table: "PatientProcedureStageTemplateMaps",
                columns: new[] { "TemplateId", "StageOrder" });

            migrationBuilder.CreateTable(
                name: "PatientProcedureAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientProcedureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientProcedureAuditEntries", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "PatientProcedureStageDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "IsArchived",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedures_TemplateId",
                table: "PatientProcedures",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureAuditEntries_AtUtc",
                table: "PatientProcedureAuditEntries",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureAuditEntries_PatientId",
                table: "PatientProcedureAuditEntries",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureAuditEntries_PatientProcedureId_AtUtc",
                table: "PatientProcedureAuditEntries",
                columns: new[] { "PatientProcedureId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureAuditEntries_UserId",
                table: "PatientProcedureAuditEntries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedures_PatientProcedureStageTemplates_TemplateId",
                table: "PatientProcedures",
                column: "TemplateId",
                principalTable: "PatientProcedureStageTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema allowed each stage only once per template and per procedure. Refuse to roll back rather
            // than delete repeated stages (a procedure's stages are a medical record).
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM PatientProcedureStageTemplateMaps
                           GROUP BY TemplateId, StageDefinitionId HAVING COUNT(*) > 1)
                   OR EXISTS (SELECT 1 FROM PatientProcedureStages
                              GROUP BY PatientProcedureId, StageDefinitionId HAVING COUNT(*) > 1)
                    THROW 50000, 'Cannot roll back AddedProcedureWorkflow: some templates or procedures repeat a stage, which the old schema does not allow. Remove the repeats first.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PatientProcedures_PatientProcedureStageTemplates_TemplateId",
                table: "PatientProcedures");

            migrationBuilder.DropTable(
                name: "PatientProcedureAuditEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PatientProcedureStageTemplateMaps",
                table: "PatientProcedureStageTemplateMaps");

            migrationBuilder.DropIndex(
                name: "IX_PatientProcedures_TemplateId",
                table: "PatientProcedures");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "PatientProcedureStageTemplates");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "StartedByUserId",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "PatientProcedureStageDefinitions");

            migrationBuilder.DropColumn(
                name: "BlockedFromStatus",
                table: "PatientProcedures");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "PatientProcedures");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "PatientProcedures");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PatientProcedureStageTemplateMaps",
                table: "PatientProcedureStageTemplateMaps",
                columns: new[] { "TemplateId", "StageDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureStageTemplateMaps_TemplateId_StageOrder",
                table: "PatientProcedureStageTemplateMaps",
                columns: new[] { "TemplateId", "StageOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureStages_PatientProcedureId_StageDefinitionId",
                table: "PatientProcedureStages",
                columns: new[] { "PatientProcedureId", "StageDefinitionId" },
                unique: true);
        }
    }
}
