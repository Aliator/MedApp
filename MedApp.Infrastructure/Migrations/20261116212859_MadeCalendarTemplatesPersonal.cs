using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// Day and week templates become each member of staff's own (<c>OwnerUserId</c>), with names unique per owner.
    /// <para>
    /// The data is moved so that nothing changes on anyone's calendar, past or future: every member of staff (an account
    /// with the Admin or User role) who uses a template, as their default week (any dated row) or a day override, gets
    /// their own copy of every template they use, with all its dated rows, and their default week rows, day overrides
    /// and block exceptions point at their copies. Templates no member of staff uses (nobody, or only people who have
    /// left: their account deleted, or every staff role taken away) go to the longest-standing admin (the earliest
    /// account created with the Admin role), or are deleted when there's no admin, since they affect no calendar anyone
    /// can open. A leaver's rows keep pointing at the template they used, whoever now owns it. See <see cref="Up"/>.
    /// </para>
    /// <para>
    /// <c>OwnerUserId</c> has no database default, so code from before this migration (old pods during a rollout, or
    /// after a rollback) can't make a template owned by nobody: its insert fails instead. It's added nullable, filled,
    /// then made required.
    /// </para>
    /// </summary>
    public partial class MadeCalendarTemplatesPersonal : Migration
    {
        /// <summary>
        /// Gives every template an owner, copying a template for each further person who uses it.
        /// <list type="number">
        /// <item>Pairs each week template with every member of staff who has ever had it as their default week, then the
        /// rest with the admin; then each day template with the owners of the week templates using it (any dated row) and
        /// every member of staff whose day overrides use it, then the rest with the admin.</item>
        /// <item>The first owner of a template (by user ID) keeps it, IDs and all; every other owner gets a copy, with
        /// copies of all its dated rows (<c>ValidFrom</c> and <c>ValidTo</c> kept). A week template's days point at
        /// their owner's copies of the day templates.</item>
        /// <item>Each person's default week rows, day overrides and block exceptions (template block to template block,
        /// old ID to new) point at their own copies.</item>
        /// <item>Without an admin, the templates left without an owner are deleted, with their rows and the default week
        /// rows and day overrides of people who have left that still point at them.</item>
        /// </list>
        /// One copy per person per template, shared by their week templates and their overrides. Names stay as they
        /// were: they were unique across the organisation, so they're unique per owner.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeekTemplates_Name",
                table: "WeekTemplates");

            migrationBuilder.DropIndex(
                name: "IX_DayTemplates_Name",
                table: "DayTemplates");

            // Nullable while it's filled, and never with a default: an insert that doesn't give an owner must fail.
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "WeekTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "DayTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(GiveTemplatesOwners);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerUserId",
                table: "WeekTemplates",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerUserId",
                table: "DayTemplates",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeekTemplates_OwnerUserId_Name",
                table: "WeekTemplates",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayTemplates_OwnerUserId_Name",
                table: "DayTemplates",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);
        }

        /// <summary>
        /// Back to organisation-wide templates, as far as that can go. Every template, copies included, becomes
        /// everyone's again, so calendars still project exactly as they did. What it can't restore: copies aren't merged
        /// back into the template they were copied from (nothing records which that was), so a template two people used
        /// stays two templates; and since names must be unique across the organisation again, each later template with a
        /// name already taken is renamed "Name (username)". Unused templates deleted on the way up don't come back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeekTemplates_OwnerUserId_Name",
                table: "WeekTemplates");

            migrationBuilder.DropIndex(
                name: "IX_DayTemplates_OwnerUserId_Name",
                table: "DayTemplates");

            migrationBuilder.Sql(MakeNamesUniqueAgain("DayTemplates"));
            migrationBuilder.Sql(MakeNamesUniqueAgain("WeekTemplates"));

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "WeekTemplates");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "DayTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_WeekTemplates_Name",
                table: "WeekTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayTemplates_Name",
                table: "DayTemplates",
                column: "Name",
                unique: true);
        }

        private const string GiveTemplatesOwners = """
            DECLARE @admin uniqueidentifier = (
                SELECT TOP (1) u.Id
                FROM AspNetUsers u
                JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                JOIN AspNetRoles r ON r.Id = ur.RoleId
                WHERE r.NormalizedName = N'ADMIN'
                ORDER BY u.CreatedAt, u.Id);

            -- The members of staff: accounts with the Admin or User role. Only they get templates; a template used only
            -- by people who have left (their account deleted, or every staff role taken away) goes to the admin, as an
            -- unused one does, rather than to someone nobody can open the templates of.
            CREATE TABLE #Staff (UserId uniqueidentifier NOT NULL PRIMARY KEY);

            INSERT INTO #Staff (UserId)
            SELECT DISTINCT ur.UserId
            FROM AspNetUserRoles ur
            JOIN AspNetRoles r ON r.Id = ur.RoleId
            WHERE r.NormalizedName IN (N'ADMIN', N'USER');

            -- Who owns which week template: every member of staff who has ever had it as their default week, then the
            -- admin.
            CREATE TABLE #WeekOwners (
                WeekTemplateId uniqueidentifier NOT NULL,
                OwnerUserId uniqueidentifier NOT NULL,
                NewId uniqueidentifier NOT NULL,
                PRIMARY KEY (WeekTemplateId, OwnerUserId));

            INSERT INTO #WeekOwners (WeekTemplateId, OwnerUserId, NewId)
            SELECT p.WeekTemplateId, p.StaffUserId, NEWID()
            FROM (
                SELECT DISTINCT s.WeekTemplateId, s.StaffUserId
                FROM StaffWeekTemplates s
                JOIN #Staff staff ON staff.UserId = s.StaffUserId) p;

            IF @admin IS NOT NULL
                INSERT INTO #WeekOwners (WeekTemplateId, OwnerUserId, NewId)
                SELECT t.Id, @admin, NEWID()
                FROM WeekTemplates t
                WHERE NOT EXISTS (SELECT 1 FROM #WeekOwners o WHERE o.WeekTemplateId = t.Id);

            -- Who owns which day template: the owners of the week templates using it (any dated row), every member of
            -- staff with a day override using it, then the admin.
            CREATE TABLE #DayOwners (
                DayTemplateId uniqueidentifier NOT NULL,
                OwnerUserId uniqueidentifier NOT NULL,
                NewId uniqueidentifier NOT NULL,
                PRIMARY KEY (DayTemplateId, OwnerUserId));

            INSERT INTO #DayOwners (DayTemplateId, OwnerUserId, NewId)
            SELECT p.DayTemplateId, p.OwnerUserId, NEWID()
            FROM (
                SELECT d.DayTemplateId, o.OwnerUserId
                FROM #WeekOwners o
                JOIN WeekTemplateDays d ON d.WeekTemplateId = o.WeekTemplateId
                UNION
                SELECT v.DayTemplateId, v.StaffUserId
                FROM CalendarDayOverrides v
                JOIN #Staff staff ON staff.UserId = v.StaffUserId
                WHERE v.DayTemplateId IS NOT NULL) p;

            IF @admin IS NOT NULL
                INSERT INTO #DayOwners (DayTemplateId, OwnerUserId, NewId)
                SELECT t.Id, @admin, NEWID()
                FROM DayTemplates t
                WHERE NOT EXISTS (SELECT 1 FROM #DayOwners o WHERE o.DayTemplateId = t.Id);

            -- The first owner of each template keeps it as it is; the others get copies.
            WITH Ranked AS (
                SELECT NewId, WeekTemplateId,
                       ROW_NUMBER() OVER (PARTITION BY WeekTemplateId ORDER BY OwnerUserId) AS N
                FROM #WeekOwners)
            UPDATE Ranked SET NewId = WeekTemplateId WHERE N = 1;

            WITH Ranked AS (
                SELECT NewId, DayTemplateId,
                       ROW_NUMBER() OVER (PARTITION BY DayTemplateId ORDER BY OwnerUserId) AS N
                FROM #DayOwners)
            UPDATE Ranked SET NewId = DayTemplateId WHERE N = 1;

            -- Day templates: the copies, with every dated row of their blocks.
            INSERT INTO DayTemplates (Id, OwnerUserId, Name, IsArchived)
            SELECT o.NewId, o.OwnerUserId, t.Name, t.IsArchived
            FROM #DayOwners o
            JOIN DayTemplates t ON t.Id = o.DayTemplateId
            WHERE o.NewId <> o.DayTemplateId;

            UPDATE t SET OwnerUserId = o.OwnerUserId
            FROM DayTemplates t
            JOIN #DayOwners o ON o.DayTemplateId = t.Id AND o.NewId = t.Id;

            CREATE TABLE #BlockCopies (
                OldId uniqueidentifier NOT NULL,
                OwnerUserId uniqueidentifier NOT NULL,
                NewId uniqueidentifier NOT NULL,
                PRIMARY KEY (OldId, OwnerUserId));

            INSERT INTO #BlockCopies (OldId, OwnerUserId, NewId)
            SELECT b.Id, o.OwnerUserId, NEWID()
            FROM #DayOwners o
            JOIN DayTemplateBlocks b ON b.DayTemplateId = o.DayTemplateId
            WHERE o.NewId <> o.DayTemplateId;

            INSERT INTO DayTemplateBlocks (Id, DayTemplateId, ActivityTypeId, StartTime, EndTime, Note, ValidFrom, ValidTo)
            SELECT c.NewId, o.NewId, b.ActivityTypeId, b.StartTime, b.EndTime, b.Note, b.ValidFrom, b.ValidTo
            FROM #BlockCopies c
            JOIN DayTemplateBlocks b ON b.Id = c.OldId
            JOIN #DayOwners o ON o.DayTemplateId = b.DayTemplateId AND o.OwnerUserId = c.OwnerUserId;

            -- Week templates: the copies, with every dated row of their days, each using its owner's own day template.
            INSERT INTO WeekTemplates (Id, OwnerUserId, Name, IsArchived)
            SELECT o.NewId, o.OwnerUserId, t.Name, t.IsArchived
            FROM #WeekOwners o
            JOIN WeekTemplates t ON t.Id = o.WeekTemplateId
            WHERE o.NewId <> o.WeekTemplateId;

            UPDATE t SET OwnerUserId = o.OwnerUserId
            FROM WeekTemplates t
            JOIN #WeekOwners o ON o.WeekTemplateId = t.Id AND o.NewId = t.Id;

            INSERT INTO WeekTemplateDays (Id, WeekTemplateId, DayOfWeek, DayTemplateId, ValidFrom, ValidTo)
            SELECT NEWID(), o.NewId, d.DayOfWeek, dayOwner.NewId, d.ValidFrom, d.ValidTo
            FROM #WeekOwners o
            JOIN WeekTemplateDays d ON d.WeekTemplateId = o.WeekTemplateId
            JOIN #DayOwners dayOwner ON dayOwner.DayTemplateId = d.DayTemplateId AND dayOwner.OwnerUserId = o.OwnerUserId
            WHERE o.NewId <> o.WeekTemplateId;

            UPDATE d SET DayTemplateId = dayOwner.NewId
            FROM WeekTemplateDays d
            JOIN #WeekOwners o ON o.WeekTemplateId = d.WeekTemplateId AND o.NewId = o.WeekTemplateId
            JOIN #DayOwners dayOwner ON dayOwner.DayTemplateId = d.DayTemplateId AND dayOwner.OwnerUserId = o.OwnerUserId;

            -- Each member of staff's calendar points at their own copies. A leaver's rows aren't owners' rows, so they
            -- keep pointing at the original, which its first owner kept.
            UPDATE s SET WeekTemplateId = o.NewId
            FROM StaffWeekTemplates s
            JOIN #WeekOwners o ON o.WeekTemplateId = s.WeekTemplateId AND o.OwnerUserId = s.StaffUserId;

            UPDATE v SET DayTemplateId = o.NewId
            FROM CalendarDayOverrides v
            JOIN #DayOwners o ON o.DayTemplateId = v.DayTemplateId AND o.OwnerUserId = v.StaffUserId;

            UPDATE e SET DayTemplateBlockId = c.NewId
            FROM CalendarBlockExceptions e
            JOIN #BlockCopies c ON c.OldId = e.DayTemplateBlockId AND c.OwnerUserId = e.StaffUserId;

            -- With no admin to take them, templates no member of staff used have no owner: they affect no calendar
            -- anyone can open, so they go, with the rows of people who have left that still point at them (their days,
            -- blocks and any exceptions to those blocks cascade).
            DELETE s
            FROM StaffWeekTemplates s
            JOIN WeekTemplates t ON t.Id = s.WeekTemplateId
            WHERE t.OwnerUserId IS NULL;

            DELETE v
            FROM CalendarDayOverrides v
            JOIN DayTemplates t ON t.Id = v.DayTemplateId
            WHERE t.OwnerUserId IS NULL;

            DELETE FROM WeekTemplates WHERE OwnerUserId IS NULL;
            DELETE FROM DayTemplates WHERE OwnerUserId IS NULL;

            DROP TABLE #BlockCopies;
            DROP TABLE #DayOwners;
            DROP TABLE #WeekOwners;
            DROP TABLE #Staff;
            """;

        /// <summary>
        /// Renames every template whose name another template already has (the first, by owner and ID, keeps it) to
        /// "Name (username)", within the 100 characters a name may have.
        /// </summary>
        private static string MakeNamesUniqueAgain(string table) => $"""
            WITH Ranked AS (
                SELECT t.Name,
                       ROW_NUMBER() OVER (PARTITION BY t.Name ORDER BY t.OwnerUserId, t.Id) AS N,
                       N' (' + COALESCE(u.UserName, CONVERT(nvarchar(36), t.OwnerUserId)) + N')' AS Suffix
                FROM {table} t
                LEFT JOIN AspNetUsers u ON u.Id = t.OwnerUserId)
            UPDATE Ranked
            SET Name = LEFT(Name, 100 - LEN(Suffix)) + Suffix
            WHERE N > 1;
            """;
    }
}
