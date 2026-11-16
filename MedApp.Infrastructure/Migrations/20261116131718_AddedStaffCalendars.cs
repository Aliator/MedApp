using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedStaffCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VisitRequirement",
                table: "PatientProcedureStages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VisitRequirement",
                table: "PatientProcedureStageDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppointmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcedureTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsAutomatic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BookedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutcomeAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutcomeByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_PatientProcedureStages_StageId",
                        column: x => x.StageId,
                        principalTable: "PatientProcedureStages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointments_PatientProcedures_ProcedureId",
                        column: x => x.ProcedureId,
                        principalTable: "PatientProcedures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CalendarActivityTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarActivityTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DayTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeekTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeekTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarBlocks_CalendarActivityTypes_ActivityTypeId",
                        column: x => x.ActivityTypeId,
                        principalTable: "CalendarActivityTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CalendarDayOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DayTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarDayOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarDayOverrides_DayTemplates_DayTemplateId",
                        column: x => x.DayTemplateId,
                        principalTable: "DayTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DayTemplateBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayTemplateBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DayTemplateBlocks_CalendarActivityTypes_ActivityTypeId",
                        column: x => x.ActivityTypeId,
                        principalTable: "CalendarActivityTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DayTemplateBlocks_DayTemplates_DayTemplateId",
                        column: x => x.DayTemplateId,
                        principalTable: "DayTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffWeekTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeekTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffWeekTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffWeekTemplates_WeekTemplates_WeekTemplateId",
                        column: x => x.WeekTemplateId,
                        principalTable: "WeekTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WeekTemplateDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeekTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    DayTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeekTemplateDays", x => x.Id);
                    table.CheckConstraint("CK_WeekTemplateDays_DayOfWeek", "[DayOfWeek] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_WeekTemplateDays_DayTemplates_DayTemplateId",
                        column: x => x.DayTemplateId,
                        principalTable: "DayTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WeekTemplateDays_WeekTemplates_WeekTemplateId",
                        column: x => x.WeekTemplateId,
                        principalTable: "WeekTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarBlockExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DayTemplateBlockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarBlockExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarBlockExceptions_DayTemplateBlocks_DayTemplateBlockId",
                        column: x => x.DayTemplateBlockId,
                        principalTable: "DayTemplateBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CalendarActivityTypes",
                columns: new[] { "Id", "Colour", "Name" },
                values: new object[,]
                {
                    { new Guid("22222222-0000-0000-0000-000000000001"), "green", "Lunch" },
                    { new Guid("22222222-0000-0000-0000-000000000002"), "blue", "Admin time" },
                    { new Guid("22222222-0000-0000-0000-000000000003"), "purple", "Training" },
                    { new Guid("22222222-0000-0000-0000-000000000004"), "teal", "Meeting" },
                    { new Guid("22222222-0000-0000-0000-000000000005"), "orange", "Annual leave" },
                    { new Guid("22222222-0000-0000-0000-000000000006"), "gray", "Travel" }
                });

            // EF also scaffolded an UpdateData with no columns for the seeded stage definition (its new
            // VisitRequirement is the column's default, None); it did nothing, so it was removed.

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentEvents_AppointmentId_AtUtc",
                table: "AppointmentEvents",
                columns: new[] { "AppointmentId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentEvents_PatientId_AtUtc",
                table: "AppointmentEvents",
                columns: new[] { "PatientId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId",
                table: "Appointments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ProcedureId",
                table: "Appointments",
                column: "ProcedureId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StaffUserId_StartUtc",
                table: "Appointments",
                columns: new[] { "StaffUserId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StageId",
                table: "Appointments",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status_CreatedAtUtc",
                table: "Appointments",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarActivityTypes_Name",
                table: "CalendarActivityTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarBlockExceptions_DayTemplateBlockId",
                table: "CalendarBlockExceptions",
                column: "DayTemplateBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarBlockExceptions_StaffUserId_Date_DayTemplateBlockId",
                table: "CalendarBlockExceptions",
                columns: new[] { "StaffUserId", "Date", "DayTemplateBlockId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarBlocks_ActivityTypeId",
                table: "CalendarBlocks",
                column: "ActivityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarBlocks_StaffUserId_StartUtc",
                table: "CalendarBlocks",
                columns: new[] { "StaffUserId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarDayOverrides_DayTemplateId",
                table: "CalendarDayOverrides",
                column: "DayTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarDayOverrides_StaffUserId_Date",
                table: "CalendarDayOverrides",
                columns: new[] { "StaffUserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayTemplateBlocks_ActivityTypeId",
                table: "DayTemplateBlocks",
                column: "ActivityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DayTemplateBlocks_DayTemplateId_ValidFrom",
                table: "DayTemplateBlocks",
                columns: new[] { "DayTemplateId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_DayTemplates_Name",
                table: "DayTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffWeekTemplates_StaffUserId_ValidFrom",
                table: "StaffWeekTemplates",
                columns: new[] { "StaffUserId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffWeekTemplates_WeekTemplateId",
                table: "StaffWeekTemplates",
                column: "WeekTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WeekTemplateDays_DayTemplateId",
                table: "WeekTemplateDays",
                column: "DayTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WeekTemplateDays_WeekTemplateId_DayOfWeek_ValidFrom",
                table: "WeekTemplateDays",
                columns: new[] { "WeekTemplateId", "DayOfWeek", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_WeekTemplates_Name",
                table: "WeekTemplates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentEvents");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "CalendarBlockExceptions");

            migrationBuilder.DropTable(
                name: "CalendarBlocks");

            migrationBuilder.DropTable(
                name: "CalendarDayOverrides");

            migrationBuilder.DropTable(
                name: "StaffWeekTemplates");

            migrationBuilder.DropTable(
                name: "WeekTemplateDays");

            migrationBuilder.DropTable(
                name: "DayTemplateBlocks");

            migrationBuilder.DropTable(
                name: "WeekTemplates");

            migrationBuilder.DropTable(
                name: "CalendarActivityTypes");

            migrationBuilder.DropTable(
                name: "DayTemplates");

            migrationBuilder.DropColumn(
                name: "VisitRequirement",
                table: "PatientProcedureStages");

            migrationBuilder.DropColumn(
                name: "VisitRequirement",
                table: "PatientProcedureStageDefinitions");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "Notifications");
        }
    }
}
