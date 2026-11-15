using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Renames the patient task tables to patient procedures in place, so existing rows are kept.
    /// EF scaffolds a rename of an entity as drop-and-create, so this migration is hand-written.
    /// Keys and foreign keys are dropped and re-added because their names include the table names.
    /// </summary>
    public partial class RenamedTasksToProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_PatientTaskAssignments_PatientTasks_PatientTaskId",
                table: "PatientTaskAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientTaskStages_PatientTaskStageDefinitions_StageDefinitionId",
                table: "PatientTaskStages");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientTaskStages_PatientTasks_PatientTaskId",
                table: "PatientTaskStages");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientTaskStageTemplateMaps_PatientTaskStageDefinitions_StageDefinitionId",
                table: "PatientTaskStageTemplateMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientTaskStageTemplateMaps_PatientTaskStageTemplates_TemplateId",
                table: "PatientTaskStageTemplateMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientTasks_Patients_PatientId",
                table: "PatientTasks");

            // Primary keys
            migrationBuilder.DropPrimaryKey(name: "PK_PatientTaskAssignments", table: "PatientTaskAssignments");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientTaskStages", table: "PatientTaskStages");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientTaskStageTemplateMaps", table: "PatientTaskStageTemplateMaps");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientTaskStageTemplates", table: "PatientTaskStageTemplates");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientTaskStageDefinitions", table: "PatientTaskStageDefinitions");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientTasks", table: "PatientTasks");

            // Tables
            migrationBuilder.RenameTable(name: "PatientTasks", newName: "PatientProcedures");
            migrationBuilder.RenameTable(name: "PatientTaskStageDefinitions", newName: "PatientProcedureStageDefinitions");
            migrationBuilder.RenameTable(name: "PatientTaskStageTemplates", newName: "PatientProcedureStageTemplates");
            migrationBuilder.RenameTable(name: "PatientTaskStageTemplateMaps", newName: "PatientProcedureStageTemplateMaps");
            migrationBuilder.RenameTable(name: "PatientTaskStages", newName: "PatientProcedureStages");
            migrationBuilder.RenameTable(name: "PatientTaskAssignments", newName: "PatientProcedureAssignments");

            // Columns
            migrationBuilder.RenameColumn(
                name: "PatientTaskId",
                table: "PatientProcedureAssignments",
                newName: "PatientProcedureId");

            migrationBuilder.RenameColumn(
                name: "PatientTaskId",
                table: "PatientProcedureStages",
                newName: "PatientProcedureId");

            // Indexes
            migrationBuilder.RenameIndex(
                name: "IX_PatientTaskAssignments_AssignedByUserId",
                table: "PatientProcedureAssignments",
                newName: "IX_PatientProcedureAssignments_AssignedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTasks_DueDateUtc",
                table: "PatientProcedures",
                newName: "IX_PatientProcedures_DueDateUtc");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTasks_PatientId_Status",
                table: "PatientProcedures",
                newName: "IX_PatientProcedures_PatientId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTaskStageDefinitions_Name",
                table: "PatientProcedureStageDefinitions",
                newName: "IX_PatientProcedureStageDefinitions_Name");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTaskStages_PatientTaskId_StageDefinitionId",
                table: "PatientProcedureStages",
                newName: "IX_PatientProcedureStages_PatientProcedureId_StageDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTaskStages_PatientTaskId_StageOrder",
                table: "PatientProcedureStages",
                newName: "IX_PatientProcedureStages_PatientProcedureId_StageOrder");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTaskStages_StageDefinitionId",
                table: "PatientProcedureStages",
                newName: "IX_PatientProcedureStages_StageDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTaskStageTemplateMaps_StageDefinitionId",
                table: "PatientProcedureStageTemplateMaps",
                newName: "IX_PatientProcedureStageTemplateMaps_StageDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTaskStageTemplateMaps_TemplateId_StageOrder",
                table: "PatientProcedureStageTemplateMaps",
                newName: "IX_PatientProcedureStageTemplateMaps_TemplateId_StageOrder");

            migrationBuilder.RenameIndex(
                name: "IX_PatientTaskStageTemplates_Name",
                table: "PatientProcedureStageTemplates",
                newName: "IX_PatientProcedureStageTemplates_Name");

            // Primary keys
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedures", table: "PatientProcedures", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureStageDefinitions", table: "PatientProcedureStageDefinitions", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureStageTemplates", table: "PatientProcedureStageTemplates", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureStageTemplateMaps", table: "PatientProcedureStageTemplateMaps", columns: new[] { "TemplateId", "StageDefinitionId" });
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureStages", table: "PatientProcedureStages", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureAssignments", table: "PatientProcedureAssignments", columns: new[] { "PatientProcedureId", "UserId" });

            // Foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedures_Patients_PatientId",
                table: "PatientProcedures",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedureAssignments_PatientProcedures_PatientProcedureId",
                table: "PatientProcedureAssignments",
                column: "PatientProcedureId",
                principalTable: "PatientProcedures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedureStages_PatientProcedureStageDefinitions_StageDefinitionId",
                table: "PatientProcedureStages",
                column: "StageDefinitionId",
                principalTable: "PatientProcedureStageDefinitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedureStages_PatientProcedures_PatientProcedureId",
                table: "PatientProcedureStages",
                column: "PatientProcedureId",
                principalTable: "PatientProcedures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedureStageTemplateMaps_PatientProcedureStageDefinitions_StageDefinitionId",
                table: "PatientProcedureStageTemplateMaps",
                column: "StageDefinitionId",
                principalTable: "PatientProcedureStageDefinitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedureStageTemplateMaps_PatientProcedureStageTemplates_TemplateId",
                table: "PatientProcedureStageTemplateMaps",
                column: "TemplateId",
                principalTable: "PatientProcedureStageTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_PatientProcedureAssignments_PatientProcedures_PatientProcedureId",
                table: "PatientProcedureAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientProcedureStages_PatientProcedureStageDefinitions_StageDefinitionId",
                table: "PatientProcedureStages");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientProcedureStages_PatientProcedures_PatientProcedureId",
                table: "PatientProcedureStages");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientProcedureStageTemplateMaps_PatientProcedureStageDefinitions_StageDefinitionId",
                table: "PatientProcedureStageTemplateMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientProcedureStageTemplateMaps_PatientProcedureStageTemplates_TemplateId",
                table: "PatientProcedureStageTemplateMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientProcedures_Patients_PatientId",
                table: "PatientProcedures");

            // Primary keys
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureAssignments", table: "PatientProcedureAssignments");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureStages", table: "PatientProcedureStages");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureStageTemplateMaps", table: "PatientProcedureStageTemplateMaps");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureStageTemplates", table: "PatientProcedureStageTemplates");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureStageDefinitions", table: "PatientProcedureStageDefinitions");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedures", table: "PatientProcedures");

            // Indexes
            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedureAssignments_AssignedByUserId",
                table: "PatientProcedureAssignments",
                newName: "IX_PatientTaskAssignments_AssignedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedures_DueDateUtc",
                table: "PatientProcedures",
                newName: "IX_PatientTasks_DueDateUtc");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedures_PatientId_Status",
                table: "PatientProcedures",
                newName: "IX_PatientTasks_PatientId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedureStageDefinitions_Name",
                table: "PatientProcedureStageDefinitions",
                newName: "IX_PatientTaskStageDefinitions_Name");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedureStages_PatientProcedureId_StageDefinitionId",
                table: "PatientProcedureStages",
                newName: "IX_PatientTaskStages_PatientTaskId_StageDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedureStages_PatientProcedureId_StageOrder",
                table: "PatientProcedureStages",
                newName: "IX_PatientTaskStages_PatientTaskId_StageOrder");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedureStages_StageDefinitionId",
                table: "PatientProcedureStages",
                newName: "IX_PatientTaskStages_StageDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedureStageTemplateMaps_StageDefinitionId",
                table: "PatientProcedureStageTemplateMaps",
                newName: "IX_PatientTaskStageTemplateMaps_StageDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedureStageTemplateMaps_TemplateId_StageOrder",
                table: "PatientProcedureStageTemplateMaps",
                newName: "IX_PatientTaskStageTemplateMaps_TemplateId_StageOrder");

            migrationBuilder.RenameIndex(
                name: "IX_PatientProcedureStageTemplates_Name",
                table: "PatientProcedureStageTemplates",
                newName: "IX_PatientTaskStageTemplates_Name");

            // Columns
            migrationBuilder.RenameColumn(
                name: "PatientProcedureId",
                table: "PatientProcedureAssignments",
                newName: "PatientTaskId");

            migrationBuilder.RenameColumn(
                name: "PatientProcedureId",
                table: "PatientProcedureStages",
                newName: "PatientTaskId");

            // Tables
            migrationBuilder.RenameTable(name: "PatientProcedures", newName: "PatientTasks");
            migrationBuilder.RenameTable(name: "PatientProcedureStageDefinitions", newName: "PatientTaskStageDefinitions");
            migrationBuilder.RenameTable(name: "PatientProcedureStageTemplates", newName: "PatientTaskStageTemplates");
            migrationBuilder.RenameTable(name: "PatientProcedureStageTemplateMaps", newName: "PatientTaskStageTemplateMaps");
            migrationBuilder.RenameTable(name: "PatientProcedureStages", newName: "PatientTaskStages");
            migrationBuilder.RenameTable(name: "PatientProcedureAssignments", newName: "PatientTaskAssignments");

            // Primary keys
            migrationBuilder.AddPrimaryKey(name: "PK_PatientTasks", table: "PatientTasks", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientTaskStageDefinitions", table: "PatientTaskStageDefinitions", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientTaskStageTemplates", table: "PatientTaskStageTemplates", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientTaskStageTemplateMaps", table: "PatientTaskStageTemplateMaps", columns: new[] { "TemplateId", "StageDefinitionId" });
            migrationBuilder.AddPrimaryKey(name: "PK_PatientTaskStages", table: "PatientTaskStages", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientTaskAssignments", table: "PatientTaskAssignments", columns: new[] { "PatientTaskId", "UserId" });

            // Foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_PatientTasks_Patients_PatientId",
                table: "PatientTasks",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTaskAssignments_PatientTasks_PatientTaskId",
                table: "PatientTaskAssignments",
                column: "PatientTaskId",
                principalTable: "PatientTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTaskStages_PatientTaskStageDefinitions_StageDefinitionId",
                table: "PatientTaskStages",
                column: "StageDefinitionId",
                principalTable: "PatientTaskStageDefinitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTaskStages_PatientTasks_PatientTaskId",
                table: "PatientTaskStages",
                column: "PatientTaskId",
                principalTable: "PatientTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTaskStageTemplateMaps_PatientTaskStageDefinitions_StageDefinitionId",
                table: "PatientTaskStageTemplateMaps",
                column: "StageDefinitionId",
                principalTable: "PatientTaskStageDefinitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientTaskStageTemplateMaps_PatientTaskStageTemplates_TemplateId",
                table: "PatientTaskStageTemplateMaps",
                column: "TemplateId",
                principalTable: "PatientTaskStageTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
