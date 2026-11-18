using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Renames procedures to pathways, and the templates of stages to pathway templates, in place, so every row is kept.
    /// EF scaffolds a rename of an entity as drop-and-create, so this migration is hand-written, as
    /// <c>RenamedTasksToProcedures</c> was: keys and foreign keys are dropped and re-added because their names include
    /// the table and column names, and tables, columns and indexes are renamed.
    /// <para>
    /// Stored numbers don't change (statuses, audit actions, notification kinds), and neither do the names written
    /// into JSON details, with one exception: what a file is attached to is stored by name in the audit entries about
    /// files, so "Procedure" there becomes "Pathway". The placeholder title of an anonymised record's procedures
    /// ("Anonymised procedure") is changed too, as it's shown to people. Notifications already sent keep their words.
    /// </para>
    /// <para>
    /// An API copy from before this migration can't read the renamed tables, so it isn't safe under a rolling
    /// deploy: stop the old copies first.
    /// </para>
    /// </summary>
    public partial class RenamedProceduresToPathways : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Foreign keys
            migrationBuilder.DropForeignKey(name: "FK_Appointments_PatientProcedureStages_StageId", table: "Appointments");
            migrationBuilder.DropForeignKey(name: "FK_Appointments_PatientProcedures_ProcedureId", table: "Appointments");
            migrationBuilder.DropForeignKey(name: "FK_PatientProcedureAssignments_PatientProcedures_PatientProcedureId", table: "PatientProcedureAssignments");
            migrationBuilder.DropForeignKey(name: "FK_PatientProcedureNotes_PatientProcedures_PatientProcedureId", table: "PatientProcedureNotes");
            migrationBuilder.DropForeignKey(name: "FK_PatientProcedureStageTemplateMaps_PatientProcedureStageDefinitions_StageDefinitionId", table: "PatientProcedureStageTemplateMaps");
            migrationBuilder.DropForeignKey(name: "FK_PatientProcedureStageTemplateMaps_PatientProcedureStageTemplates_TemplateId", table: "PatientProcedureStageTemplateMaps");
            migrationBuilder.DropForeignKey(name: "FK_PatientProcedureStages_PatientProcedureStageDefinitions_StageDefinitionId", table: "PatientProcedureStages");
            migrationBuilder.DropForeignKey(name: "FK_PatientProcedureStages_PatientProcedures_PatientProcedureId", table: "PatientProcedureStages");
            migrationBuilder.DropForeignKey(name: "FK_PatientProcedures_PatientProcedureStageTemplates_TemplateId", table: "PatientProcedures");
            migrationBuilder.DropForeignKey(name: "FK_PatientProcedures_Patients_PatientId", table: "PatientProcedures");

            // Primary keys
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureAssignments", table: "PatientProcedureAssignments");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureAuditEntries", table: "PatientProcedureAuditEntries");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureNoteVersions", table: "PatientProcedureNoteVersions");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureNotes", table: "PatientProcedureNotes");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureStageDefinitions", table: "PatientProcedureStageDefinitions");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureStageTemplateMaps", table: "PatientProcedureStageTemplateMaps");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureStageTemplates", table: "PatientProcedureStageTemplates");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedureStages", table: "PatientProcedureStages");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientProcedures", table: "PatientProcedures");
            migrationBuilder.DropPrimaryKey(name: "PK_ProcedureNoteDeletionRequests", table: "ProcedureNoteDeletionRequests");

            // Tables
            migrationBuilder.RenameTable(name: "PatientProcedureAssignments", newName: "PatientPathwayAssignments");
            migrationBuilder.RenameTable(name: "PatientProcedureAuditEntries", newName: "PatientPathwayAuditEntries");
            migrationBuilder.RenameTable(name: "PatientProcedureNoteVersions", newName: "PatientPathwayNoteVersions");
            migrationBuilder.RenameTable(name: "PatientProcedureNotes", newName: "PatientPathwayNotes");
            migrationBuilder.RenameTable(name: "PatientProcedureStageDefinitions", newName: "PatientPathwayStageDefinitions");
            migrationBuilder.RenameTable(name: "PatientProcedureStageTemplateMaps", newName: "PathwayTemplateMaps");
            migrationBuilder.RenameTable(name: "PatientProcedureStageTemplates", newName: "PathwayTemplates");
            migrationBuilder.RenameTable(name: "PatientProcedureStages", newName: "PatientPathwayStages");
            migrationBuilder.RenameTable(name: "PatientProcedures", newName: "PatientPathways");
            migrationBuilder.RenameTable(name: "ProcedureNoteDeletionRequests", newName: "PathwayNoteDeletionRequests");

            // Columns
            migrationBuilder.RenameColumn(name: "ProcedureId", table: "ActivityEntries", newName: "PathwayId");
            migrationBuilder.RenameColumn(name: "ProcedureId", table: "AppointmentEvents", newName: "PathwayId");
            migrationBuilder.RenameColumn(name: "ProcedureTitle", table: "AppointmentEvents", newName: "PathwayTitle");
            migrationBuilder.RenameColumn(name: "ProcedureId", table: "Appointments", newName: "PathwayId");
            migrationBuilder.RenameColumn(name: "ProcedureId", table: "Attachments", newName: "PathwayId");
            migrationBuilder.RenameColumn(name: "ProcedureAssigned", table: "NotificationPreferences", newName: "PathwayAssigned");
            migrationBuilder.RenameColumn(name: "ProcedureId", table: "Notifications", newName: "PathwayId");
            migrationBuilder.RenameColumn(name: "PatientProcedureId", table: "PatientPathwayAssignments", newName: "PatientPathwayId");
            migrationBuilder.RenameColumn(name: "PatientProcedureId", table: "PatientPathwayAuditEntries", newName: "PatientPathwayId");
            migrationBuilder.RenameColumn(name: "ProcedureTitle", table: "PatientPathwayAuditEntries", newName: "PathwayTitle");
            migrationBuilder.RenameColumn(name: "PatientProcedureId", table: "PatientPathwayNoteVersions", newName: "PatientPathwayId");
            migrationBuilder.RenameColumn(name: "PatientProcedureId", table: "PatientPathwayNotes", newName: "PatientPathwayId");
            migrationBuilder.RenameColumn(name: "PatientProcedureId", table: "PatientPathwayStages", newName: "PatientPathwayId");
            migrationBuilder.RenameColumn(name: "PatientProcedureId", table: "PathwayNoteDeletionRequests", newName: "PatientPathwayId");

            // Indexes
            migrationBuilder.RenameIndex(name: "IX_Appointments_ProcedureId", table: "Appointments", newName: "IX_Appointments_PathwayId");
            migrationBuilder.RenameIndex(name: "IX_Attachments_ProcedureId", table: "Attachments", newName: "IX_Attachments_PathwayId");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureAssignments_AssignedByUserId", table: "PatientPathwayAssignments", newName: "IX_PatientPathwayAssignments_AssignedByUserId");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureAuditEntries_AtUtc", table: "PatientPathwayAuditEntries", newName: "IX_PatientPathwayAuditEntries_AtUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureAuditEntries_PatientId_AtUtc", table: "PatientPathwayAuditEntries", newName: "IX_PatientPathwayAuditEntries_PatientId_AtUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureAuditEntries_PatientProcedureId_AtUtc", table: "PatientPathwayAuditEntries", newName: "IX_PatientPathwayAuditEntries_PatientPathwayId_AtUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureAuditEntries_UserId", table: "PatientPathwayAuditEntries", newName: "IX_PatientPathwayAuditEntries_UserId");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureNoteVersions_NoteId_Version", table: "PatientPathwayNoteVersions", newName: "IX_PatientPathwayNoteVersions_NoteId_Version");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureNoteVersions_PatientId", table: "PatientPathwayNoteVersions", newName: "IX_PatientPathwayNoteVersions_PatientId");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureNotes_PatientProcedureId_CreatedAtUtc", table: "PatientPathwayNotes", newName: "IX_PatientPathwayNotes_PatientPathwayId_CreatedAtUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureStageDefinitions_Name", table: "PatientPathwayStageDefinitions", newName: "IX_PatientPathwayStageDefinitions_Name");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureStageTemplateMaps_StageDefinitionId", table: "PathwayTemplateMaps", newName: "IX_PathwayTemplateMaps_StageDefinitionId");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureStageTemplates_Name", table: "PathwayTemplates", newName: "IX_PathwayTemplates_Name");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureStages_PatientProcedureId_StageOrder", table: "PatientPathwayStages", newName: "IX_PatientPathwayStages_PatientPathwayId_StageOrder");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedureStages_StageDefinitionId", table: "PatientPathwayStages", newName: "IX_PatientPathwayStages_StageDefinitionId");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedures_DueDateUtc", table: "PatientPathways", newName: "IX_PatientPathways_DueDateUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedures_PatientId_Status", table: "PatientPathways", newName: "IX_PatientPathways_PatientId_Status");
            migrationBuilder.RenameIndex(name: "IX_PatientProcedures_TemplateId", table: "PatientPathways", newName: "IX_PatientPathways_TemplateId");
            migrationBuilder.RenameIndex(name: "IX_ProcedureNoteDeletionRequests_NoteId_Pending", table: "PathwayNoteDeletionRequests", newName: "IX_PathwayNoteDeletionRequests_NoteId_Pending");
            migrationBuilder.RenameIndex(name: "IX_ProcedureNoteDeletionRequests_PatientId", table: "PathwayNoteDeletionRequests", newName: "IX_PathwayNoteDeletionRequests_PatientId");
            migrationBuilder.RenameIndex(name: "IX_ProcedureNoteDeletionRequests_PatientProcedureId_Status", table: "PathwayNoteDeletionRequests", newName: "IX_PathwayNoteDeletionRequests_PatientPathwayId_Status");

            // Primary keys
            migrationBuilder.AddPrimaryKey(name: "PK_PatientPathwayAssignments", table: "PatientPathwayAssignments", columns: new[] { "PatientPathwayId", "UserId" });
            migrationBuilder.AddPrimaryKey(name: "PK_PatientPathwayAuditEntries", table: "PatientPathwayAuditEntries", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientPathwayNoteVersions", table: "PatientPathwayNoteVersions", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientPathwayNotes", table: "PatientPathwayNotes", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientPathwayStageDefinitions", table: "PatientPathwayStageDefinitions", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PathwayTemplateMaps", table: "PathwayTemplateMaps", columns: new[] { "TemplateId", "StageOrder" });
            migrationBuilder.AddPrimaryKey(name: "PK_PathwayTemplates", table: "PathwayTemplates", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientPathwayStages", table: "PatientPathwayStages", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientPathways", table: "PatientPathways", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PathwayNoteDeletionRequests", table: "PathwayNoteDeletionRequests", column: "Id");

            // Foreign keys, each with the delete behaviour it had
            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_PatientPathwayStages_StageId",
                table: "Appointments",
                column: "StageId",
                principalTable: "PatientPathwayStages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_PatientPathways_PathwayId",
                table: "Appointments",
                column: "PathwayId",
                principalTable: "PatientPathways",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathwayAssignments_PatientPathways_PatientPathwayId",
                table: "PatientPathwayAssignments",
                column: "PatientPathwayId",
                principalTable: "PatientPathways",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathwayNotes_PatientPathways_PatientPathwayId",
                table: "PatientPathwayNotes",
                column: "PatientPathwayId",
                principalTable: "PatientPathways",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PathwayTemplateMaps_PatientPathwayStageDefinitions_StageDefinitionId",
                table: "PathwayTemplateMaps",
                column: "StageDefinitionId",
                principalTable: "PatientPathwayStageDefinitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PathwayTemplateMaps_PathwayTemplates_TemplateId",
                table: "PathwayTemplateMaps",
                column: "TemplateId",
                principalTable: "PathwayTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathwayStages_PatientPathwayStageDefinitions_StageDefinitionId",
                table: "PatientPathwayStages",
                column: "StageDefinitionId",
                principalTable: "PatientPathwayStageDefinitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathwayStages_PatientPathways_PatientPathwayId",
                table: "PatientPathwayStages",
                column: "PatientPathwayId",
                principalTable: "PatientPathways",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathways_PathwayTemplates_TemplateId",
                table: "PatientPathways",
                column: "TemplateId",
                principalTable: "PathwayTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathways_Patients_PatientId",
                table: "PatientPathways",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id");

            // What a file is attached to, by name, in the audit entries about files (the enum member was renamed).
            migrationBuilder.Sql(
                """
                UPDATE PatientPathwayAuditEntries
                SET Details = REPLACE(Details, N'"attachedTo":"Procedure"', N'"attachedTo":"Pathway"')
                WHERE Action IN (10, 11, 12) AND Details LIKE N'%"attachedTo":"Procedure"%';
                """);

            // The title an anonymised record's procedures were given when they had no template's name to take.
            migrationBuilder.Sql(
                """
                UPDATE p SET Title = N'Anonymised pathway'
                FROM PatientPathways p JOIN Patients patient ON patient.Id = p.PatientId
                WHERE patient.AnonymisedAtUtc IS NOT NULL AND p.Title = N'Anonymised procedure';

                UPDATE e SET PathwayTitle = N'Anonymised pathway'
                FROM PatientPathwayAuditEntries e JOIN Patients patient ON patient.Id = e.PatientId
                WHERE patient.AnonymisedAtUtc IS NOT NULL AND e.PathwayTitle = N'Anonymised procedure';

                UPDATE e SET PathwayTitle = N'Anonymised pathway'
                FROM AppointmentEvents e JOIN Patients patient ON patient.Id = e.PatientId
                WHERE patient.AnonymisedAtUtc IS NOT NULL AND e.PathwayTitle = N'Anonymised procedure';

                UPDATE Notifications SET Title = N'Anonymised pathway'
                WHERE Kind = 0 AND Title = N'Anonymised procedure';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // What a file is attached to, by name, in the audit entries about files (the enum member was renamed).
            migrationBuilder.Sql(
                """
                UPDATE PatientPathwayAuditEntries
                SET Details = REPLACE(Details, N'"attachedTo":"Pathway"', N'"attachedTo":"Procedure"')
                WHERE Action IN (10, 11, 12) AND Details LIKE N'%"attachedTo":"Pathway"%';
                """);

            // The title an anonymised record's pathways were given when they had no template's name to take.
            migrationBuilder.Sql(
                """
                UPDATE p SET Title = N'Anonymised procedure'
                FROM PatientPathways p JOIN Patients patient ON patient.Id = p.PatientId
                WHERE patient.AnonymisedAtUtc IS NOT NULL AND p.Title = N'Anonymised pathway';

                UPDATE e SET PathwayTitle = N'Anonymised procedure'
                FROM PatientPathwayAuditEntries e JOIN Patients patient ON patient.Id = e.PatientId
                WHERE patient.AnonymisedAtUtc IS NOT NULL AND e.PathwayTitle = N'Anonymised pathway';

                UPDATE e SET PathwayTitle = N'Anonymised procedure'
                FROM AppointmentEvents e JOIN Patients patient ON patient.Id = e.PatientId
                WHERE patient.AnonymisedAtUtc IS NOT NULL AND e.PathwayTitle = N'Anonymised pathway';

                UPDATE Notifications SET Title = N'Anonymised procedure'
                WHERE Kind = 0 AND Title = N'Anonymised pathway';
                """);

            // Foreign keys
            migrationBuilder.DropForeignKey(name: "FK_Appointments_PatientPathwayStages_StageId", table: "Appointments");
            migrationBuilder.DropForeignKey(name: "FK_Appointments_PatientPathways_PathwayId", table: "Appointments");
            migrationBuilder.DropForeignKey(name: "FK_PatientPathwayAssignments_PatientPathways_PatientPathwayId", table: "PatientPathwayAssignments");
            migrationBuilder.DropForeignKey(name: "FK_PatientPathwayNotes_PatientPathways_PatientPathwayId", table: "PatientPathwayNotes");
            migrationBuilder.DropForeignKey(name: "FK_PathwayTemplateMaps_PatientPathwayStageDefinitions_StageDefinitionId", table: "PathwayTemplateMaps");
            migrationBuilder.DropForeignKey(name: "FK_PathwayTemplateMaps_PathwayTemplates_TemplateId", table: "PathwayTemplateMaps");
            migrationBuilder.DropForeignKey(name: "FK_PatientPathwayStages_PatientPathwayStageDefinitions_StageDefinitionId", table: "PatientPathwayStages");
            migrationBuilder.DropForeignKey(name: "FK_PatientPathwayStages_PatientPathways_PatientPathwayId", table: "PatientPathwayStages");
            migrationBuilder.DropForeignKey(name: "FK_PatientPathways_PathwayTemplates_TemplateId", table: "PatientPathways");
            migrationBuilder.DropForeignKey(name: "FK_PatientPathways_Patients_PatientId", table: "PatientPathways");

            // Primary keys
            migrationBuilder.DropPrimaryKey(name: "PK_PatientPathwayAssignments", table: "PatientPathwayAssignments");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientPathwayAuditEntries", table: "PatientPathwayAuditEntries");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientPathwayNoteVersions", table: "PatientPathwayNoteVersions");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientPathwayNotes", table: "PatientPathwayNotes");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientPathwayStageDefinitions", table: "PatientPathwayStageDefinitions");
            migrationBuilder.DropPrimaryKey(name: "PK_PathwayTemplateMaps", table: "PathwayTemplateMaps");
            migrationBuilder.DropPrimaryKey(name: "PK_PathwayTemplates", table: "PathwayTemplates");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientPathwayStages", table: "PatientPathwayStages");
            migrationBuilder.DropPrimaryKey(name: "PK_PatientPathways", table: "PatientPathways");
            migrationBuilder.DropPrimaryKey(name: "PK_PathwayNoteDeletionRequests", table: "PathwayNoteDeletionRequests");

            // Tables
            migrationBuilder.RenameTable(name: "PatientPathwayAssignments", newName: "PatientProcedureAssignments");
            migrationBuilder.RenameTable(name: "PatientPathwayAuditEntries", newName: "PatientProcedureAuditEntries");
            migrationBuilder.RenameTable(name: "PatientPathwayNoteVersions", newName: "PatientProcedureNoteVersions");
            migrationBuilder.RenameTable(name: "PatientPathwayNotes", newName: "PatientProcedureNotes");
            migrationBuilder.RenameTable(name: "PatientPathwayStageDefinitions", newName: "PatientProcedureStageDefinitions");
            migrationBuilder.RenameTable(name: "PathwayTemplateMaps", newName: "PatientProcedureStageTemplateMaps");
            migrationBuilder.RenameTable(name: "PathwayTemplates", newName: "PatientProcedureStageTemplates");
            migrationBuilder.RenameTable(name: "PatientPathwayStages", newName: "PatientProcedureStages");
            migrationBuilder.RenameTable(name: "PatientPathways", newName: "PatientProcedures");
            migrationBuilder.RenameTable(name: "PathwayNoteDeletionRequests", newName: "ProcedureNoteDeletionRequests");

            // Columns
            migrationBuilder.RenameColumn(name: "PathwayId", table: "ActivityEntries", newName: "ProcedureId");
            migrationBuilder.RenameColumn(name: "PathwayId", table: "AppointmentEvents", newName: "ProcedureId");
            migrationBuilder.RenameColumn(name: "PathwayTitle", table: "AppointmentEvents", newName: "ProcedureTitle");
            migrationBuilder.RenameColumn(name: "PathwayId", table: "Appointments", newName: "ProcedureId");
            migrationBuilder.RenameColumn(name: "PathwayId", table: "Attachments", newName: "ProcedureId");
            migrationBuilder.RenameColumn(name: "PathwayAssigned", table: "NotificationPreferences", newName: "ProcedureAssigned");
            migrationBuilder.RenameColumn(name: "PathwayId", table: "Notifications", newName: "ProcedureId");
            migrationBuilder.RenameColumn(name: "PatientPathwayId", table: "PatientProcedureAssignments", newName: "PatientProcedureId");
            migrationBuilder.RenameColumn(name: "PatientPathwayId", table: "PatientProcedureAuditEntries", newName: "PatientProcedureId");
            migrationBuilder.RenameColumn(name: "PathwayTitle", table: "PatientProcedureAuditEntries", newName: "ProcedureTitle");
            migrationBuilder.RenameColumn(name: "PatientPathwayId", table: "PatientProcedureNoteVersions", newName: "PatientProcedureId");
            migrationBuilder.RenameColumn(name: "PatientPathwayId", table: "PatientProcedureNotes", newName: "PatientProcedureId");
            migrationBuilder.RenameColumn(name: "PatientPathwayId", table: "PatientProcedureStages", newName: "PatientProcedureId");
            migrationBuilder.RenameColumn(name: "PatientPathwayId", table: "ProcedureNoteDeletionRequests", newName: "PatientProcedureId");

            // Indexes
            migrationBuilder.RenameIndex(name: "IX_Appointments_PathwayId", table: "Appointments", newName: "IX_Appointments_ProcedureId");
            migrationBuilder.RenameIndex(name: "IX_Attachments_PathwayId", table: "Attachments", newName: "IX_Attachments_ProcedureId");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayAssignments_AssignedByUserId", table: "PatientProcedureAssignments", newName: "IX_PatientProcedureAssignments_AssignedByUserId");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayAuditEntries_AtUtc", table: "PatientProcedureAuditEntries", newName: "IX_PatientProcedureAuditEntries_AtUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayAuditEntries_PatientId_AtUtc", table: "PatientProcedureAuditEntries", newName: "IX_PatientProcedureAuditEntries_PatientId_AtUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayAuditEntries_PatientPathwayId_AtUtc", table: "PatientProcedureAuditEntries", newName: "IX_PatientProcedureAuditEntries_PatientProcedureId_AtUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayAuditEntries_UserId", table: "PatientProcedureAuditEntries", newName: "IX_PatientProcedureAuditEntries_UserId");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayNoteVersions_NoteId_Version", table: "PatientProcedureNoteVersions", newName: "IX_PatientProcedureNoteVersions_NoteId_Version");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayNoteVersions_PatientId", table: "PatientProcedureNoteVersions", newName: "IX_PatientProcedureNoteVersions_PatientId");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayNotes_PatientPathwayId_CreatedAtUtc", table: "PatientProcedureNotes", newName: "IX_PatientProcedureNotes_PatientProcedureId_CreatedAtUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayStageDefinitions_Name", table: "PatientProcedureStageDefinitions", newName: "IX_PatientProcedureStageDefinitions_Name");
            migrationBuilder.RenameIndex(name: "IX_PathwayTemplateMaps_StageDefinitionId", table: "PatientProcedureStageTemplateMaps", newName: "IX_PatientProcedureStageTemplateMaps_StageDefinitionId");
            migrationBuilder.RenameIndex(name: "IX_PathwayTemplates_Name", table: "PatientProcedureStageTemplates", newName: "IX_PatientProcedureStageTemplates_Name");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayStages_PatientPathwayId_StageOrder", table: "PatientProcedureStages", newName: "IX_PatientProcedureStages_PatientProcedureId_StageOrder");
            migrationBuilder.RenameIndex(name: "IX_PatientPathwayStages_StageDefinitionId", table: "PatientProcedureStages", newName: "IX_PatientProcedureStages_StageDefinitionId");
            migrationBuilder.RenameIndex(name: "IX_PatientPathways_DueDateUtc", table: "PatientProcedures", newName: "IX_PatientProcedures_DueDateUtc");
            migrationBuilder.RenameIndex(name: "IX_PatientPathways_PatientId_Status", table: "PatientProcedures", newName: "IX_PatientProcedures_PatientId_Status");
            migrationBuilder.RenameIndex(name: "IX_PatientPathways_TemplateId", table: "PatientProcedures", newName: "IX_PatientProcedures_TemplateId");
            migrationBuilder.RenameIndex(name: "IX_PathwayNoteDeletionRequests_NoteId_Pending", table: "ProcedureNoteDeletionRequests", newName: "IX_ProcedureNoteDeletionRequests_NoteId_Pending");
            migrationBuilder.RenameIndex(name: "IX_PathwayNoteDeletionRequests_PatientId", table: "ProcedureNoteDeletionRequests", newName: "IX_ProcedureNoteDeletionRequests_PatientId");
            migrationBuilder.RenameIndex(name: "IX_PathwayNoteDeletionRequests_PatientPathwayId_Status", table: "ProcedureNoteDeletionRequests", newName: "IX_ProcedureNoteDeletionRequests_PatientProcedureId_Status");

            // Primary keys
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureAssignments", table: "PatientProcedureAssignments", columns: new[] { "PatientProcedureId", "UserId" });
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureAuditEntries", table: "PatientProcedureAuditEntries", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureNoteVersions", table: "PatientProcedureNoteVersions", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureNotes", table: "PatientProcedureNotes", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureStageDefinitions", table: "PatientProcedureStageDefinitions", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureStageTemplateMaps", table: "PatientProcedureStageTemplateMaps", columns: new[] { "TemplateId", "StageOrder" });
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureStageTemplates", table: "PatientProcedureStageTemplates", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedureStages", table: "PatientProcedureStages", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_PatientProcedures", table: "PatientProcedures", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_ProcedureNoteDeletionRequests", table: "ProcedureNoteDeletionRequests", column: "Id");

            // Foreign keys, each with the delete behaviour it had
            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_PatientProcedureStages_StageId",
                table: "Appointments",
                column: "StageId",
                principalTable: "PatientProcedureStages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_PatientProcedures_ProcedureId",
                table: "Appointments",
                column: "ProcedureId",
                principalTable: "PatientProcedures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedureAssignments_PatientProcedures_PatientProcedureId",
                table: "PatientProcedureAssignments",
                column: "PatientProcedureId",
                principalTable: "PatientProcedures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedureNotes_PatientProcedures_PatientProcedureId",
                table: "PatientProcedureNotes",
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
                name: "FK_PatientProcedures_PatientProcedureStageTemplates_TemplateId",
                table: "PatientProcedures",
                column: "TemplateId",
                principalTable: "PatientProcedureStageTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientProcedures_Patients_PatientId",
                table: "PatientProcedures",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id");
        }
    }
}
