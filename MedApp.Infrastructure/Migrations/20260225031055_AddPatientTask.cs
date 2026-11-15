using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Patients",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PatientTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientTasks_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PatientTaskStageDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTaskStageDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientTaskStageTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTaskStageTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientTaskAssignments",
                columns: table => new
                {
                    PatientTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTaskAssignments", x => new { x.PatientTaskId, x.UserId });
                    table.ForeignKey(
                        name: "FK_PatientTaskAssignments_PatientTasks_PatientTaskId",
                        column: x => x.PatientTaskId,
                        principalTable: "PatientTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientTaskStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageOrder = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTaskStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientTaskStages_PatientTaskStageDefinitions_StageDefinitionId",
                        column: x => x.StageDefinitionId,
                        principalTable: "PatientTaskStageDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatientTaskStages_PatientTasks_PatientTaskId",
                        column: x => x.PatientTaskId,
                        principalTable: "PatientTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientTaskStageTemplateMaps",
                columns: table => new
                {
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTaskStageTemplateMaps", x => new { x.TemplateId, x.StageDefinitionId });
                    table.ForeignKey(
                        name: "FK_PatientTaskStageTemplateMaps_PatientTaskStageDefinitions_StageDefinitionId",
                        column: x => x.StageDefinitionId,
                        principalTable: "PatientTaskStageDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PatientTaskStageTemplateMaps_PatientTaskStageTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PatientTaskStageTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PatientTaskStageDefinitions",
                columns: new[] { "Id", "Description", "Instructions", "Name" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "This stage definition was deleted.", "No instructions available.", "Definition missing" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientTaskAssignments_AssignedByUserId",
                table: "PatientTaskAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTasks_DueDateUtc",
                table: "PatientTasks",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTasks_PatientId_Status",
                table: "PatientTasks",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientTaskStageDefinitions_Name",
                table: "PatientTaskStageDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientTaskStages_PatientTaskId_StageDefinitionId",
                table: "PatientTaskStages",
                columns: new[] { "PatientTaskId", "StageDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientTaskStages_PatientTaskId_StageOrder",
                table: "PatientTaskStages",
                columns: new[] { "PatientTaskId", "StageOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientTaskStages_StageDefinitionId",
                table: "PatientTaskStages",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTaskStageTemplateMaps_StageDefinitionId",
                table: "PatientTaskStageTemplateMaps",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTaskStageTemplateMaps_TemplateId_StageOrder",
                table: "PatientTaskStageTemplateMaps",
                columns: new[] { "TemplateId", "StageOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientTaskStageTemplates_Name",
                table: "PatientTaskStageTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_ExpiresAtUtc",
                table: "UserSessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientTaskAssignments");

            migrationBuilder.DropTable(
                name: "PatientTaskStages");

            migrationBuilder.DropTable(
                name: "PatientTaskStageTemplateMaps");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "PatientTasks");

            migrationBuilder.DropTable(
                name: "PatientTaskStageDefinitions");

            migrationBuilder.DropTable(
                name: "PatientTaskStageTemplates");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Patients",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);
        }
    }
}
