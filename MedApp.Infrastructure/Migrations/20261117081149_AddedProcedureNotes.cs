using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Procedures hold several notes: <c>PatientProcedureNotes</c>, and every version of each in
    /// <c>PatientProcedureNoteVersions</c> (no foreign keys, like the audit log). Each procedure's old single notes field,
    /// if it has any text, becomes its first note: by whoever created the procedure (its Created audit entry), or by
    /// nobody ("Unknown") when that wasn't recorded, at the procedure's creation, as version 1.
    /// <para>
    /// The old <c>Notes</c> column stays, unused, so API copies from before this keep working during a rollout (the
    /// chart's two-release rule); a later release drops it. Down drops the new tables: notes added or edited since are
    /// lost, and the old column still holds what it held.
    /// </para>
    /// </summary>
    public partial class AddedProcedureNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientProcedureNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientProcedureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastEditedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientProcedureNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientProcedureNotes_PatientProcedures_PatientProcedureId",
                        column: x => x.PatientProcedureId,
                        principalTable: "PatientProcedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientProcedureNoteVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PatientProcedureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientProcedureNoteVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureNotes_PatientProcedureId_CreatedAtUtc",
                table: "PatientProcedureNotes",
                columns: new[] { "PatientProcedureId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureNoteVersions_NoteId_Version",
                table: "PatientProcedureNoteVersions",
                columns: new[] { "NoteId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientProcedureNoteVersions_PatientId",
                table: "PatientProcedureNoteVersions",
                column: "PatientId");

            // Each procedure's old notes, if any text, as its first note. The author is the user of its Created audit
            // entry (Action 0), the first if there were ever two; without one, nobody (and see the edits below). Blank
            // notes (spaces, tabs or line breaks only) are left out.
            migrationBuilder.Sql("""
                INSERT INTO PatientProcedureNotes (Id, PatientProcedureId, AuthorUserId, Text, CreatedAtUtc, LastEditedAtUtc, Version)
                SELECT NEWID(), p.Id, created.UserId, p.Notes, p.CreatedAt, NULL, 1
                FROM PatientProcedures p
                OUTER APPLY (
                    SELECT TOP (1) a.UserId
                    FROM PatientProcedureAuditEntries a
                    WHERE a.PatientProcedureId = p.Id AND a.Action = 0
                    ORDER BY a.AtUtc, a.Id
                ) created
                WHERE TRIM(CHAR(9) + CHAR(10) + CHAR(13) + CHAR(32) FROM p.Notes) <> N'';
                """);

            // The old field could be edited by anyone, so its text may not all be the creator's. Each edit of it is a
            // DetailsUpdated audit entry (Action 1) with changes.notes. When anyone but the creator edited it, its author
            // is unknown (null: shown as "Unknown", and nobody can edit it), rather than crediting the creator with
            // someone else's words. Either way, an edited note is marked as edited, at the last edit.
            migrationBuilder.Sql("""
                WITH NotesEdits AS (
                    SELECT a.PatientProcedureId, a.UserId, a.AtUtc
                    FROM PatientProcedureAuditEntries a
                    WHERE a.Action = 1
                      AND CASE WHEN ISJSON(a.Details) = 1 THEN JSON_QUERY(a.Details, '$.changes.notes') END IS NOT NULL
                )
                UPDATE n
                SET LastEditedAtUtc = (SELECT MAX(e.AtUtc) FROM NotesEdits e WHERE e.PatientProcedureId = n.PatientProcedureId),
                    AuthorUserId = CASE
                        WHEN EXISTS (
                            SELECT 1 FROM NotesEdits e
                            WHERE e.PatientProcedureId = n.PatientProcedureId
                              AND (n.AuthorUserId IS NULL OR e.UserId <> n.AuthorUserId))
                        THEN NULL
                        ELSE n.AuthorUserId
                    END
                FROM PatientProcedureNotes n
                WHERE EXISTS (SELECT 1 FROM NotesEdits e WHERE e.PatientProcedureId = n.PatientProcedureId);
                """);

            // And its first version: by its author, with the username the Created entry recorded (empty when the author
            // is unknown), at the last edit of the old field, or its creation if it was never edited.
            migrationBuilder.Sql("""
                INSERT INTO PatientProcedureNoteVersions (Id, NoteId, Version, PatientProcedureId, PatientId, Text, UserId, Username, AtUtc)
                SELECT NEWID(), n.Id, 1, n.PatientProcedureId, p.PatientId, n.Text, n.AuthorUserId,
                       CASE WHEN n.AuthorUserId IS NULL THEN N'' ELSE COALESCE(created.Username, N'') END,
                       COALESCE(n.LastEditedAtUtc, n.CreatedAtUtc)
                FROM PatientProcedureNotes n
                JOIN PatientProcedures p ON p.Id = n.PatientProcedureId
                OUTER APPLY (
                    SELECT TOP (1) a.Username
                    FROM PatientProcedureAuditEntries a
                    WHERE a.PatientProcedureId = p.Id AND a.Action = 0
                    ORDER BY a.AtUtc, a.Id
                ) created;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientProcedureNotes");

            migrationBuilder.DropTable(
                name: "PatientProcedureNoteVersions");
        }
    }
}
