using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedNoteDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "PatientProcedureNotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "PatientProcedureNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionDetail",
                table: "PatientProcedureNotes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletionReason",
                table: "PatientProcedureNotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingDeletionRequestId",
                table: "PatientProcedureNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NoteDeletionRequestId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcedureNoteDeletionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientProcedureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcedureNoteDeletionRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureNoteDeletionRequests_NoteId_Pending",
                table: "ProcedureNoteDeletionRequests",
                column: "NoteId",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureNoteDeletionRequests_PatientId",
                table: "ProcedureNoteDeletionRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureNoteDeletionRequests_PatientProcedureId_Status",
                table: "ProcedureNoteDeletionRequests",
                columns: new[] { "PatientProcedureId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcedureNoteDeletionRequests");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "PatientProcedureNotes");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "PatientProcedureNotes");

            migrationBuilder.DropColumn(
                name: "DeletionDetail",
                table: "PatientProcedureNotes");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "PatientProcedureNotes");

            migrationBuilder.DropColumn(
                name: "PendingDeletionRequestId",
                table: "PatientProcedureNotes");

            migrationBuilder.DropColumn(
                name: "NoteDeletionRequestId",
                table: "Notifications");
        }
    }
}
