using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Renames specialties to teams (docs/multi-clinic-plan.md, MC2) in place, so every row is kept: the tables
    /// (Specialties to Teams, StaffSpecialties to TeamMemberships), every SpecialtyId column to TeamId, and their keys
    /// and indexes. EF scaffolds a renamed entity as drop-and-create, so the two tables' part is written by hand, as
    /// <c>RenamedProceduresToPathways</c> was. The defaults (General, for templates and pathways) go with the columns.
    /// <para>
    /// Stored names in JSON details keep the old word (a referral's history's <c>specialtyId</c> and
    /// <c>specialtyName</c>, an MDT audit entry's <c>specialty</c>), as do account settings; stored numbers don't
    /// change. An API copy from before this migration can't read the renamed tables: stop the old copies first.
    /// </para>
    /// </summary>
    public partial class RenamedSpecialtiesToTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicPatterns_Specialties_SpecialtyId",
                table: "ClinicPatterns");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicSessions_Specialties_SpecialtyId",
                table: "ClinicSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterTemplates_Specialties_SpecialtyId",
                table: "LetterTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_MdtItems_Specialties_SpecialtyId",
                table: "MdtItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Specialties_SpecialtyId",
                table: "Meetings");

            migrationBuilder.DropForeignKey(
                name: "FK_PathwayTemplates_Specialties_SpecialtyId",
                table: "PathwayTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPathways_Specialties_SpecialtyId",
                table: "PatientPathways");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_Specialties_SpecialtyId",
                table: "Referrals");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "Referrals",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_Referrals_Status_SpecialtyId_WaitStartOn",
                table: "Referrals",
                newName: "IX_Referrals_Status_TeamId_WaitStartOn");

            migrationBuilder.RenameIndex(
                name: "IX_Referrals_SpecialtyId",
                table: "Referrals",
                newName: "IX_Referrals_TeamId");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "PatientPathways",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientPathways_SpecialtyId",
                table: "PatientPathways",
                newName: "IX_PatientPathways_TeamId");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "PathwayTemplates",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_PathwayTemplates_SpecialtyId",
                table: "PathwayTemplates",
                newName: "IX_PathwayTemplates_TeamId");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "MonitoringRules",
                newName: "TeamId");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "Meetings",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_Meetings_SpecialtyId",
                table: "Meetings",
                newName: "IX_Meetings_TeamId");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "MdtItems",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_MdtItems_SpecialtyId_Status",
                table: "MdtItems",
                newName: "IX_MdtItems_TeamId_Status");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "LetterTemplates",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_LetterTemplates_SpecialtyId_Name",
                table: "LetterTemplates",
                newName: "IX_LetterTemplates_TeamId_Name");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "ClinicSessions",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_ClinicSessions_SpecialtyId",
                table: "ClinicSessions",
                newName: "IX_ClinicSessions_TeamId");

            migrationBuilder.RenameColumn(
                name: "SpecialtyId",
                table: "ClinicPatterns",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_ClinicPatterns_SpecialtyId",
                table: "ClinicPatterns",
                newName: "IX_ClinicPatterns_TeamId");

            // The memberships and the teams themselves: renamed in place, with their keys (their names include the table).
            migrationBuilder.DropForeignKey(name: "FK_StaffSpecialties_AspNetUsers_UserId", table: "StaffSpecialties");
            migrationBuilder.DropForeignKey(name: "FK_StaffSpecialties_Specialties_SpecialtyId", table: "StaffSpecialties");
            migrationBuilder.DropPrimaryKey(name: "PK_StaffSpecialties", table: "StaffSpecialties");
            migrationBuilder.DropPrimaryKey(name: "PK_Specialties", table: "Specialties");
            migrationBuilder.RenameTable(name: "Specialties", newName: "Teams");
            migrationBuilder.RenameTable(name: "StaffSpecialties", newName: "TeamMemberships");
            migrationBuilder.RenameColumn(name: "SpecialtyId", table: "TeamMemberships", newName: "TeamId");
            migrationBuilder.RenameIndex(name: "IX_Specialties_Name", table: "Teams", newName: "IX_Teams_Name");
            migrationBuilder.RenameIndex(name: "IX_StaffSpecialties_SpecialtyId", table: "TeamMemberships", newName: "IX_TeamMemberships_TeamId");
            migrationBuilder.AddPrimaryKey(name: "PK_Teams", table: "Teams", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_TeamMemberships", table: "TeamMemberships", columns: new[] { "UserId", "TeamId" });
            migrationBuilder.AddForeignKey(name: "FK_TeamMemberships_AspNetUsers_UserId", table: "TeamMemberships", column: "UserId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_TeamMemberships_Teams_TeamId", table: "TeamMemberships", column: "TeamId", principalTable: "Teams", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicPatterns_Teams_TeamId",
                table: "ClinicPatterns",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicSessions_Teams_TeamId",
                table: "ClinicSessions",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterTemplates_Teams_TeamId",
                table: "LetterTemplates",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MdtItems_Teams_TeamId",
                table: "MdtItems",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Teams_TeamId",
                table: "Meetings",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PathwayTemplates_Teams_TeamId",
                table: "PathwayTemplates",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathways_Teams_TeamId",
                table: "PatientPathways",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_Teams_TeamId",
                table: "Referrals",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicPatterns_Teams_TeamId",
                table: "ClinicPatterns");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicSessions_Teams_TeamId",
                table: "ClinicSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_LetterTemplates_Teams_TeamId",
                table: "LetterTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_MdtItems_Teams_TeamId",
                table: "MdtItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Teams_TeamId",
                table: "Meetings");

            migrationBuilder.DropForeignKey(
                name: "FK_PathwayTemplates_Teams_TeamId",
                table: "PathwayTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPathways_Teams_TeamId",
                table: "PatientPathways");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_Teams_TeamId",
                table: "Referrals");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "Referrals",
                newName: "SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_Referrals_TeamId",
                table: "Referrals",
                newName: "IX_Referrals_SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_Referrals_Status_TeamId_WaitStartOn",
                table: "Referrals",
                newName: "IX_Referrals_Status_SpecialtyId_WaitStartOn");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "PatientPathways",
                newName: "SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_PatientPathways_TeamId",
                table: "PatientPathways",
                newName: "IX_PatientPathways_SpecialtyId");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "PathwayTemplates",
                newName: "SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_PathwayTemplates_TeamId",
                table: "PathwayTemplates",
                newName: "IX_PathwayTemplates_SpecialtyId");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "MonitoringRules",
                newName: "SpecialtyId");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "Meetings",
                newName: "SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_Meetings_TeamId",
                table: "Meetings",
                newName: "IX_Meetings_SpecialtyId");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "MdtItems",
                newName: "SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_MdtItems_TeamId_Status",
                table: "MdtItems",
                newName: "IX_MdtItems_SpecialtyId_Status");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "LetterTemplates",
                newName: "SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_LetterTemplates_TeamId_Name",
                table: "LetterTemplates",
                newName: "IX_LetterTemplates_SpecialtyId_Name");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "ClinicSessions",
                newName: "SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_ClinicSessions_TeamId",
                table: "ClinicSessions",
                newName: "IX_ClinicSessions_SpecialtyId");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "ClinicPatterns",
                newName: "SpecialtyId");

            migrationBuilder.RenameIndex(
                name: "IX_ClinicPatterns_TeamId",
                table: "ClinicPatterns",
                newName: "IX_ClinicPatterns_SpecialtyId");

            // The memberships and the teams themselves: renamed in place, with their keys (their names include the table).
            migrationBuilder.DropForeignKey(name: "FK_TeamMemberships_AspNetUsers_UserId", table: "TeamMemberships");
            migrationBuilder.DropForeignKey(name: "FK_TeamMemberships_Teams_TeamId", table: "TeamMemberships");
            migrationBuilder.DropPrimaryKey(name: "PK_TeamMemberships", table: "TeamMemberships");
            migrationBuilder.DropPrimaryKey(name: "PK_Teams", table: "Teams");
            migrationBuilder.RenameTable(name: "Teams", newName: "Specialties");
            migrationBuilder.RenameTable(name: "TeamMemberships", newName: "StaffSpecialties");
            migrationBuilder.RenameColumn(name: "TeamId", table: "StaffSpecialties", newName: "SpecialtyId");
            migrationBuilder.RenameIndex(name: "IX_Teams_Name", table: "Specialties", newName: "IX_Specialties_Name");
            migrationBuilder.RenameIndex(name: "IX_TeamMemberships_TeamId", table: "StaffSpecialties", newName: "IX_StaffSpecialties_SpecialtyId");
            migrationBuilder.AddPrimaryKey(name: "PK_Specialties", table: "Specialties", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_StaffSpecialties", table: "StaffSpecialties", columns: new[] { "UserId", "SpecialtyId" });
            migrationBuilder.AddForeignKey(name: "FK_StaffSpecialties_AspNetUsers_UserId", table: "StaffSpecialties", column: "UserId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_StaffSpecialties_Specialties_SpecialtyId", table: "StaffSpecialties", column: "SpecialtyId", principalTable: "Specialties", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicPatterns_Specialties_SpecialtyId",
                table: "ClinicPatterns",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicSessions_Specialties_SpecialtyId",
                table: "ClinicSessions",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LetterTemplates_Specialties_SpecialtyId",
                table: "LetterTemplates",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MdtItems_Specialties_SpecialtyId",
                table: "MdtItems",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Specialties_SpecialtyId",
                table: "Meetings",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PathwayTemplates_Specialties_SpecialtyId",
                table: "PathwayTemplates",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPathways_Specialties_SpecialtyId",
                table: "PatientPathways",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_Specialties_SpecialtyId",
                table: "Referrals",
                column: "SpecialtyId",
                principalTable: "Specialties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
