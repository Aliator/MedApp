using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// The multi-clinic centre's one combined migration (MC3 to MC29 of <c>docs/multi-clinic-plan.md</c>, built demo
    /// first, so its migrations <c>AddedTeamKindsAndAccountTypes</c> to <c>AddedClinicalPhotoFlag</c> are this one).
    /// <list type="bullet">
    /// <item><b>Schema:</b> team kinds and membership roles; a team on every clinical row (decision 200); pathway grants
    /// (<c>PathwayAccess</c>); stage owners, pick-up and handover (the relay); stage links, plans, addable branches and
    /// choice nodes (tree pathways); lab tests, lab cases and their steps, samples reworked (source, requesting clinic,
    /// lab case and test; no pathway for a hospital's); hospitals, hospital requests and surgery details; MDT presenters;
    /// consults; clinical photos.</item>
    /// <item><b>Dropped on purpose:</b> the external labs list (<c>Laboratories</c>, <c>Specimens.LaboratoryId</c>,
    /// MC13); the unique index on a pathway's stage orders.</item>
    /// <item><b>Data:</b> General and the lab team; the Lab role's holders join the lab, and every other account with a
    /// role but no team (admins aside) joins General; the Pathologist role; "All staff (before roles)" archived (its
    /// holders keep its permissions); every new <c>TeamId</c> backfilled; Owner grants for every pathway, the lab's
    /// StageOwner grant on each with a sample, and MDT attendees' grants for open items; samples from a clinic; branch
    /// points become choice nodes (<see cref="TemplateChoiceNodesSql"/>, <see cref="PathwayChoiceNodesSql"/>).</item>
    /// </list>
    /// Down puts back what the older model can hold: branch points from choice nodes, one placeholder lab for every
    /// sample, and the stage-order index; hospital samples (which have no pathway) and everything new go.
    /// </summary>
    public partial class AddedMultiClinicCentre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MC13: the external labs list goes, on purpose (the lab is a team now). A specimen's lab was only a pointer
            // to that list; the audit log and timeline keep the name as written. EF scaffolded this as a rename of
            // LaboratoryId to TeamId, which would have kept lab IDs as team IDs: the column is dropped, and TeamId is
            // added and backfilled from the pathway below.
            migrationBuilder.DropForeignKey(
                name: "FK_Specimens_Laboratories_LaboratoryId",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_Status_LaboratoryId_SentAtUtc",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_LaboratoryId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "LaboratoryId",
                table: "Specimens");

            migrationBuilder.DropTable(
                name: "Laboratories");

            // MC8: a pathway's stage orders are a display order now (a grown pathway renumbers), so not unique. The
            // index comes back below without uniqueness.
            migrationBuilder.DropIndex(
                name: "IX_PatientPathwayStages_PatientPathwayId_StageOrder",
                table: "PatientPathwayStages");

            // MC10b: a stage's recorded outcome. EF scaffolded this as a rename of Branch (the branch a stage belonged
            // to), which is a different thing: Outcome is added, filled from the branch chosen, and Branch dropped below.
            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "PatientPathwayStages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Teams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "TeamMemberships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SnomedCode",
                table: "SpecimenTypes",
                type: "varchar(18)",
                unicode: false,
                maxLength: 18,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StageId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "PathwayId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "HospitalId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HospitalRequestId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LabCaseId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LabTestId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestingTeamId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Specimens",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LabCaseId",
                table: "Results",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportText",
                table: "Results",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Results",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "ReferralEvents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BranchAdditionId",
                table: "PatientPathwayStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChoiceKind",
                table: "PatientPathwayStages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HandedOverFromTeamId",
                table: "PatientPathwayStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerTeamId",
                table: "PatientPathwayStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickedUpAtUtc",
                table: "PatientPathwayStages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickedUpByUserId",
                table: "PatientPathwayStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReachedAtUtc",
                table: "PatientPathwayStages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultEnteredAtUtc",
                table: "PatientPathwayStages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultEnteredByUserId",
                table: "PatientPathwayStages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultText",
                table: "PatientPathwayStages",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateStageOrder",
                table: "PatientPathwayStages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "PatientPathways",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "PatientPathwayAuditEntries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "PatientMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "AddableBranches",
                table: "PathwayTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValueSql: "N'[]'");

            migrationBuilder.AddColumn<string>(
                name: "Links",
                table: "PathwayTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValueSql: "N'[]'");

            migrationBuilder.AddColumn<int>(
                name: "ChoiceKind",
                table: "PathwayTemplateMaps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcomes",
                table: "PathwayTemplateMaps",
                type: "nvarchar(max)",
                nullable: false,
                defaultValueSql: "N'[]'");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerTeamId",
                table: "PathwayTemplateMaps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PresenterUserId",
                table: "MdtItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresenterUsername",
                table: "MdtItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Letters",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "ConsentRecords",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsClinicalPhoto",
                table: "Attachments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "AppointmentEvents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Consults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AskingTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AskedTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reply = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RepliedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RepliedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RepliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedByUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consults_PatientPathways_PathwayId",
                        column: x => x.PathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Consults_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consults_Teams_AskedTeamId",
                        column: x => x.AskedTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Consults_Teams_AskingTeamId",
                        column: x => x.AskingTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Hospitals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OdsCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Contact = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReportDeliveryMethod = table.Column<int>(type: "int", nullable: false),
                    DefaultDeadlineDays = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hospitals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SnomedCode = table.Column<string>(type: "varchar(18)", unicode: false, maxLength: 18, nullable: true),
                    TargetDays = table.Column<int>(type: "int", nullable: false),
                    LimitDays = table.Column<int>(type: "int", nullable: false),
                    AtRiskDays = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PathwayAccess",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    MdtItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PathwayAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PathwayAccess_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PathwayBranchAdditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientPathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AfterStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PathwayBranchAdditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PathwayBranchAdditions_PatientPathways_PatientPathwayId",
                        column: x => x.PatientPathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientPathwayStageLinks",
                columns: table => new
                {
                    FromStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientPathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPathwayStageLinks", x => new { x.FromStageId, x.ToStageId });
                    table.ForeignKey(
                        name: "FK_PatientPathwayStageLinks_PatientPathways_PatientPathwayId",
                        column: x => x.PatientPathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HospitalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HospitalPatientNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NhsNumber = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    SurgeonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsCancerPathway = table.Column<bool>(type: "bit", nullable: false),
                    AuthoredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportNeededBy = table.Column<DateOnly>(type: "date", nullable: true),
                    CollectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SpecimenTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabTestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabCaseNumber = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HospitalRequests_Hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalTable: "Hospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HospitalRequests_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HospitalRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HospitalRequests_SpecimenTypes_SpecimenTypeId",
                        column: x => x.SpecimenTypeId,
                        principalTable: "SpecimenTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HospitalRequests_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LabTestSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabTestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabTestSteps_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    LabTestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    RequestingTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HospitalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HospitalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PathwayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RepeatDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RepeatRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepeatRequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnteredInErrorAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnteredInErrorByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportSentByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportAddressedTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReportDeliveryMethod = table.Column<int>(type: "int", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCases_HospitalRequests_HospitalRequestId",
                        column: x => x.HospitalRequestId,
                        principalTable: "HospitalRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabCases_Hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalTable: "Hospitals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabCases_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabCases_PatientPathwayStages_StageId",
                        column: x => x.StageId,
                        principalTable: "PatientPathwayStages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabCases_PatientPathways_PathwayId",
                        column: x => x.PathwayId,
                        principalTable: "PatientPathways",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabCases_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabCases_Teams_RequestingTeamId",
                        column: x => x.RequestingTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LabCases_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LabCaseSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    TestStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Choice = table.Column<int>(type: "int", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnteredInErrorAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnteredInErrorByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCaseSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCaseSteps_LabCases_LabCaseId",
                        column: x => x.LabCaseId,
                        principalTable: "LabCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabCaseSurgeryDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurgeryOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Procedure = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NoSurgeryExpected = table.Column<bool>(type: "bit", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCaseSurgeryDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabCaseSurgeryDetails_LabCases_LabCaseId",
                        column: x => x.LabCaseId,
                        principalTable: "LabCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // The data steps, in order: General and the lab team, memberships, roles, every row's team, the grants, and
            // branch points into choice nodes. Each is one batch, written here so it never changes after it ships.
            migrationBuilder.Sql(TeamsSql);
            migrationBuilder.Sql(MembershipsSql);
            migrationBuilder.Sql(RolesSql);
            migrationBuilder.Sql(TeamBackfillSql);
            migrationBuilder.Sql(GrantsSql);
            migrationBuilder.Sql(TemplateChoiceNodesSql);
            migrationBuilder.Sql(PathwayChoiceNodesSql);

            migrationBuilder.DropColumn(
                name: "ChoosesBranch",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "Branch",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "Branch",
                table: "PathwayTemplateMaps");

            migrationBuilder.DropColumn(
                name: "ChoosesBranch",
                table: "PathwayTemplateMaps");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_TeamId",
                table: "Specimens",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OneLab",
                table: "Teams",
                column: "Kind",
                unique: true,
                filter: "[Kind] = 1 AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_HospitalId",
                table: "Specimens",
                column: "HospitalId",
                filter: "[HospitalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_HospitalRequestId",
                table: "Specimens",
                column: "HospitalRequestId",
                filter: "[HospitalRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_LabCaseId",
                table: "Specimens",
                column: "LabCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_LabTestId",
                table: "Specimens",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_RequestingTeamId",
                table: "Specimens",
                column: "RequestingTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_Status_SentAtUtc",
                table: "Specimens",
                columns: new[] { "Status", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Results_LabCaseId",
                table: "Results",
                column: "LabCaseId",
                filter: "[LabCaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Results_TeamId",
                table: "Results",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralEvents_TeamId",
                table: "ReferralEvents",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPathwayStages_HandedOverFromTeamId",
                table: "PatientPathwayStages",
                column: "HandedOverFromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPathwayStages_OwnerTeamId",
                table: "PatientPathwayStages",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPathwayStages_PatientPathwayId_StageOrder",
                table: "PatientPathwayStages",
                columns: new[] { "PatientPathwayId", "StageOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPathwayAuditEntries_TeamId",
                table: "PatientPathwayAuditEntries",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMessages_TeamId",
                table: "PatientMessages",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PathwayTemplateMaps_OwnerTeamId",
                table: "PathwayTemplateMaps",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Letters_TeamId",
                table: "Letters",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_TeamId",
                table: "ConsentRecords",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TeamId",
                table: "Attachments",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TeamId",
                table: "Appointments",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentEvents_TeamId",
                table: "AppointmentEvents",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Consults_AskedTeamId_Status",
                table: "Consults",
                columns: new[] { "AskedTeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Consults_AskingTeamId",
                table: "Consults",
                column: "AskingTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Consults_PathwayId",
                table: "Consults",
                column: "PathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_Consults_PathwayId_AskedTeamId_Open",
                table: "Consults",
                columns: new[] { "PathwayId", "AskedTeamId" },
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_Consults_PatientId",
                table: "Consults",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalRequests_HospitalId",
                table: "HospitalRequests",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalRequests_LabCaseId",
                table: "HospitalRequests",
                column: "LabCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalRequests_LabTestId",
                table: "HospitalRequests",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalRequests_PatientId",
                table: "HospitalRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalRequests_RegisteredAtUtc",
                table: "HospitalRequests",
                column: "RegisteredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalRequests_SpecimenTypeId",
                table: "HospitalRequests",
                column: "SpecimenTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalRequests_TeamId",
                table: "HospitalRequests",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_Name",
                table: "Hospitals",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hospitals_OdsCode",
                table: "Hospitals",
                column: "OdsCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_HospitalId",
                table: "LabCases",
                column: "HospitalId",
                filter: "[HospitalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_HospitalRequestId",
                table: "LabCases",
                column: "HospitalRequestId",
                filter: "[HospitalRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_LabTestId",
                table: "LabCases",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_Number",
                table: "LabCases",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_PathwayId",
                table: "LabCases",
                column: "PathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_PatientId",
                table: "LabCases",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_RequestingTeamId",
                table: "LabCases",
                column: "RequestingTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_StageId",
                table: "LabCases",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_Status_OpenedAtUtc",
                table: "LabCases",
                columns: new[] { "Status", "OpenedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LabCases_TeamId",
                table: "LabCases",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCaseSteps_LabCaseId_Sequence",
                table: "LabCaseSteps",
                columns: new[] { "LabCaseId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabCaseSurgeryDetails_LabCaseId",
                table: "LabCaseSurgeryDetails",
                column: "LabCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_Name",
                table: "LabTests",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTestSteps_LabTestId_Position",
                table: "LabTestSteps",
                columns: new[] { "LabTestId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_PathwayAccess_MdtItemId",
                table: "PathwayAccess",
                column: "MdtItemId",
                filter: "[MdtItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PathwayAccess_PathwayId_TeamId",
                table: "PathwayAccess",
                columns: new[] { "PathwayId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_PathwayAccess_PathwayId_UserId",
                table: "PathwayAccess",
                columns: new[] { "PathwayId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PathwayAccess_PatientId",
                table: "PathwayAccess",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PathwayAccess_TeamId_EndedAtUtc",
                table: "PathwayAccess",
                columns: new[] { "TeamId", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PathwayBranchAdditions_PatientId",
                table: "PathwayBranchAdditions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PathwayBranchAdditions_PatientPathwayId",
                table: "PathwayBranchAdditions",
                column: "PatientPathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPathwayStageLinks_PatientPathwayId",
                table: "PatientPathwayStageLinks",
                column: "PatientPathwayId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPathwayStageLinks_ToStageId",
                table: "PatientPathwayStageLinks",
                column: "ToStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentEvents_Teams_TeamId",
                table: "AppointmentEvents",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Teams_TeamId",
                table: "Appointments",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Teams_TeamId",
                table: "Attachments",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsentRecords_Teams_TeamId",
                table: "ConsentRecords",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Letters_Teams_TeamId",
                table: "Letters",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PathwayTemplateMaps_Teams_OwnerTeamId",
                table: "PathwayTemplateMaps",
                column: "OwnerTeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientMessages_Teams_TeamId",
                table: "PatientMessages",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathwayAuditEntries_Teams_TeamId",
                table: "PatientPathwayAuditEntries",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathwayStages_Teams_HandedOverFromTeamId",
                table: "PatientPathwayStages",
                column: "HandedOverFromTeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathwayStages_Teams_OwnerTeamId",
                table: "PatientPathwayStages",
                column: "OwnerTeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReferralEvents_Teams_TeamId",
                table: "ReferralEvents",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Results_LabCases_LabCaseId",
                table: "Results",
                column: "LabCaseId",
                principalTable: "LabCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Results_Teams_TeamId",
                table: "Results",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Specimens_HospitalRequests_HospitalRequestId",
                table: "Specimens",
                column: "HospitalRequestId",
                principalTable: "HospitalRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Specimens_Hospitals_HospitalId",
                table: "Specimens",
                column: "HospitalId",
                principalTable: "Hospitals",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Specimens_LabCases_LabCaseId",
                table: "Specimens",
                column: "LabCaseId",
                principalTable: "LabCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Specimens_LabTests_LabTestId",
                table: "Specimens",
                column: "LabTestId",
                principalTable: "LabTests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Specimens_Teams_RequestingTeamId",
                table: "Specimens",
                column: "RequestingTeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Specimens_Teams_TeamId",
                table: "Specimens",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old columns and the labs list come back first, so the data can go into them.
            migrationBuilder.AddColumn<bool>(
                name: "ChoosesBranch",
                table: "PatientPathwayStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Branch",
                table: "PatientPathwayStages",
                type: "nvarchar(50)",
                maxLength: 50,
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

            migrationBuilder.CreateTable(
                name: "Laboratories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UsesMedApp = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laboratories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Laboratories_Name",
                table: "Laboratories",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LaboratoryId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(DownSql);

            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentEvents_Teams_TeamId",
                table: "AppointmentEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Teams_TeamId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Teams_TeamId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsentRecords_Teams_TeamId",
                table: "ConsentRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Letters_Teams_TeamId",
                table: "Letters");

            migrationBuilder.DropForeignKey(
                name: "FK_PathwayTemplateMaps_Teams_OwnerTeamId",
                table: "PathwayTemplateMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientMessages_Teams_TeamId",
                table: "PatientMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPathwayAuditEntries_Teams_TeamId",
                table: "PatientPathwayAuditEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPathwayStages_Teams_HandedOverFromTeamId",
                table: "PatientPathwayStages");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPathwayStages_Teams_OwnerTeamId",
                table: "PatientPathwayStages");

            migrationBuilder.DropForeignKey(
                name: "FK_ReferralEvents_Teams_TeamId",
                table: "ReferralEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Results_LabCases_LabCaseId",
                table: "Results");

            migrationBuilder.DropForeignKey(
                name: "FK_Results_Teams_TeamId",
                table: "Results");

            migrationBuilder.DropForeignKey(
                name: "FK_Specimens_HospitalRequests_HospitalRequestId",
                table: "Specimens");

            migrationBuilder.DropForeignKey(
                name: "FK_Specimens_Hospitals_HospitalId",
                table: "Specimens");

            migrationBuilder.DropForeignKey(
                name: "FK_Specimens_LabCases_LabCaseId",
                table: "Specimens");

            migrationBuilder.DropForeignKey(
                name: "FK_Specimens_LabTests_LabTestId",
                table: "Specimens");

            migrationBuilder.DropForeignKey(
                name: "FK_Specimens_Teams_RequestingTeamId",
                table: "Specimens");

            migrationBuilder.DropForeignKey(
                name: "FK_Specimens_Teams_TeamId",
                table: "Specimens");

            migrationBuilder.DropTable(
                name: "Consults");

            migrationBuilder.DropTable(
                name: "LabCaseSteps");

            migrationBuilder.DropTable(
                name: "LabCaseSurgeryDetails");

            migrationBuilder.DropTable(
                name: "LabTestSteps");

            migrationBuilder.DropTable(
                name: "PathwayAccess");

            migrationBuilder.DropTable(
                name: "PathwayBranchAdditions");

            migrationBuilder.DropTable(
                name: "PatientPathwayStageLinks");

            migrationBuilder.DropTable(
                name: "LabCases");

            migrationBuilder.DropTable(
                name: "HospitalRequests");

            migrationBuilder.DropTable(
                name: "Hospitals");

            migrationBuilder.DropTable(
                name: "LabTests");

            migrationBuilder.DropIndex(
                name: "IX_Teams_OneLab",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_HospitalId",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_HospitalRequestId",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_LabCaseId",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_LabTestId",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_RequestingTeamId",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_Status_SentAtUtc",
                table: "Specimens");

            migrationBuilder.DropIndex(
                name: "IX_Results_LabCaseId",
                table: "Results");

            migrationBuilder.DropIndex(
                name: "IX_Results_TeamId",
                table: "Results");

            migrationBuilder.DropIndex(
                name: "IX_ReferralEvents_TeamId",
                table: "ReferralEvents");

            migrationBuilder.DropIndex(
                name: "IX_PatientPathwayStages_HandedOverFromTeamId",
                table: "PatientPathwayStages");

            migrationBuilder.DropIndex(
                name: "IX_PatientPathwayStages_OwnerTeamId",
                table: "PatientPathwayStages");

            migrationBuilder.DropIndex(
                name: "IX_PatientPathwayStages_PatientPathwayId_StageOrder",
                table: "PatientPathwayStages");

            migrationBuilder.DropIndex(
                name: "IX_PatientPathwayAuditEntries_TeamId",
                table: "PatientPathwayAuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_PatientMessages_TeamId",
                table: "PatientMessages");

            migrationBuilder.DropIndex(
                name: "IX_PathwayTemplateMaps_OwnerTeamId",
                table: "PathwayTemplateMaps");

            migrationBuilder.DropIndex(
                name: "IX_Letters_TeamId",
                table: "Letters");

            migrationBuilder.DropIndex(
                name: "IX_ConsentRecords_TeamId",
                table: "ConsentRecords");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_TeamId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_TeamId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentEvents_TeamId",
                table: "AppointmentEvents");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "TeamMemberships");

            migrationBuilder.DropColumn(
                name: "SnomedCode",
                table: "SpecimenTypes");

            migrationBuilder.DropColumn(
                name: "HospitalId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "HospitalRequestId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "LabCaseId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "LabTestId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "RequestingTeamId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "LabCaseId",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "ReportText",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "ReferralEvents");

            migrationBuilder.DropColumn(
                name: "BranchAdditionId",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "ChoiceKind",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "HandedOverFromTeamId",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "OwnerTeamId",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "PickedUpAtUtc",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "PickedUpByUserId",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "ReachedAtUtc",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "ResultEnteredAtUtc",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "ResultEnteredByUserId",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "ResultText",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "TemplateStageOrder",
                table: "PatientPathwayStages");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "PatientPathways");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "PatientPathwayAuditEntries");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "PatientMessages");

            migrationBuilder.DropColumn(
                name: "AddableBranches",
                table: "PathwayTemplates");

            migrationBuilder.DropColumn(
                name: "Links",
                table: "PathwayTemplates");

            migrationBuilder.DropColumn(
                name: "ChoiceKind",
                table: "PathwayTemplateMaps");

            migrationBuilder.DropColumn(
                name: "Outcomes",
                table: "PathwayTemplateMaps");

            migrationBuilder.DropColumn(
                name: "OwnerTeamId",
                table: "PathwayTemplateMaps");

            migrationBuilder.DropColumn(
                name: "PresenterUserId",
                table: "MdtItems");

            migrationBuilder.DropColumn(
                name: "PresenterUsername",
                table: "MdtItems");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "ConsentRecords");

            migrationBuilder.DropColumn(
                name: "IsClinicalPhoto",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "AppointmentEvents");

            migrationBuilder.DropIndex(
                name: "IX_Specimens_TeamId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Specimens");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "PatientPathwayStages");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPathwayStages_PatientPathwayId_StageOrder",
                table: "PatientPathwayStages",
                columns: new[] { "PatientPathwayId", "StageOrder" },
                unique: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "LaboratoryId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StageId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PathwayId",
                table: "Specimens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_LaboratoryId",
                table: "Specimens",
                column: "LaboratoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Specimens_Status_LaboratoryId_SentAtUtc",
                table: "Specimens",
                columns: new[] { "Status", "LaboratoryId", "SentAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Specimens_Laboratories_LaboratoryId",
                table: "Specimens",
                column: "LaboratoryId",
                principalTable: "Laboratories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
