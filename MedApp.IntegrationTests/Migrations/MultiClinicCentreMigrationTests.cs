using System.Text.Json;
using FluentAssertions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>AddedMultiClinicCentre</c> against a real SQL Server: the lab team, memberships and roles; every new team column
/// backfilled; the Owner, StageOwner and Mdt grants; and branch points turned into choice nodes (a template's outcomes
/// and links, a chosen branch recorded as the outcome, a pathway still to choose grown from a plan, one with work after
/// its branch point left as a chain). Down puts the branches back, and up again works. The rows are synthetic, inserted
/// with SQL at the migration before, and read with SQL (AGENTS.md, Migrations).
/// </summary>
[TestFixture]
public sealed class MultiClinicCentreMigrationTests
{
    private const string Before = "20261118233909_RenamedSpecialtiesToTeams";
    private const string Migration = "20261119192009_AddedMultiClinicCentre";

    private static readonly Guid Clinic = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private static readonly Guid OldLab = Guid.Parse("d0000000-0000-0000-0000-000000000002");
    private static readonly Guid Clinician = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid LabMember = Guid.Parse("e0000000-0000-0000-0000-000000000002");
    private static readonly Guid Admin = Guid.Parse("e0000000-0000-0000-0000-000000000003");
    private static readonly Guid AllStaff = Guid.Parse("e0000000-0000-0000-0000-000000000005");
    private static readonly Guid BranchingTemplate = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ChainTemplate = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid Grown = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid Chosen = Guid.Parse("b1000000-0000-0000-0000-000000000002");
    private static readonly Guid Cancelled = Guid.Parse("b1000000-0000-0000-0000-000000000003");
    private static readonly Guid WorkAfter = Guid.Parse("b1000000-0000-0000-0000-000000000004");
    private static readonly Guid Specimen = Guid.Parse("a6000000-0000-0000-0000-000000000001");
    private static readonly Guid MdtItem = Guid.Parse("a9000000-0000-0000-0000-000000000001");
    private static readonly Guid General = Domain.Teams.Team.GeneralId;
    private static readonly Guid Lab = Domain.Teams.Team.LabId;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A clinic and a team already called "Lab"; a clinician, a Lab member and an "All staff" holder in no team, and an
    /// admin and a nurse in the clinic; a template with a branch point (stage 2: Benign, or Suspicious written twice in two
    /// cases) and a chain; four pathways from it (still to choose, chosen as Suspicious with Benign skipped, cancelled,
    /// and still to choose with a visit booked after it); a sample, two visits, a letter with no pathway and its audit
    /// entry, and an MDT item still listed with the Lab member invited.
    /// </summary>
    private const string Seed = """
        SET NOCOUNT ON;
        DECLARE @now datetime2 = '2026-11-01T10:00:00';
        DECLARE @def uniqueidentifier = '11111111-1111-1111-1111-111111111111';
        DECLARE @derm uniqueidentifier = 'd0000000-0000-0000-0000-000000000001';
        DECLARE @oldLab uniqueidentifier = 'd0000000-0000-0000-0000-000000000002';
        DECLARE @u1 uniqueidentifier = 'e0000000-0000-0000-0000-000000000001';
        DECLARE @u2 uniqueidentifier = 'e0000000-0000-0000-0000-000000000002';
        DECLARE @u3 uniqueidentifier = 'e0000000-0000-0000-0000-000000000003';
        DECLARE @u4 uniqueidentifier = 'e0000000-0000-0000-0000-000000000004';
        DECLARE @u5 uniqueidentifier = 'e0000000-0000-0000-0000-000000000005';
        DECLARE @pt uniqueidentifier = 'f0000000-0000-0000-0000-000000000001';
        DECLARE @t1 uniqueidentifier = 'a1000000-0000-0000-0000-000000000001';
        DECLARE @t2 uniqueidentifier = 'a1000000-0000-0000-0000-000000000002';

        INSERT INTO Teams (Id, Name, IsArchived, Version, TargetDaysLow, TargetDaysMedium, TargetDaysHigh, TargetDaysUrgent, CreatedAt, LastUpdated)
        VALUES (@derm, N'Synthetic clinic', 0, 1, 126, 126, 28, 14, @now, @now),
               (@oldLab, N'Lab', 0, 1, 126, 126, 28, 14, @now, @now);

        INSERT INTO AspNetUsers (Id, UserName, CreatedAt, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
        VALUES (@u1, N'synthetic.clinician', @now, 0, 0, 0, 0, 0), (@u2, N'synthetic.lab', @now, 0, 0, 0, 0, 0),
               (@u3, N'synthetic.admin', @now, 0, 0, 0, 0, 0), (@u4, N'synthetic.nurse', @now, 0, 0, 0, 0, 0),
               (@u5, N'synthetic.staff', @now, 0, 0, 0, 0, 0);
        INSERT INTO AspNetUserRoles (UserId, RoleId)
        VALUES (@u1, '7a3b0000-0000-4000-8000-000000000003'), (@u2, '7a3b0000-0000-4000-8000-000000000006'),
               (@u3, '7a3b0000-0000-4000-8000-000000000001'), (@u4, '7a3b0000-0000-4000-8000-000000000004'),
               (@u5, '7a3b0000-0000-4000-8000-000000000002');
        INSERT INTO TeamMemberships (UserId, TeamId) VALUES (@u3, @derm), (@u4, @derm);

        INSERT INTO Patients (Id, FirstName, LastName, DateOfBirth, CreatedAt, LastUpdated)
        VALUES (@pt, N'Synthetic', N'Patient', '1980-01-01', @now, @now);

        -- A template with a branch point (stage 2), two branches (one written in two cases) and a stage after them; and a chain.
        INSERT INTO PathwayTemplates (Id, Name, TeamId) VALUES (@t1, N'Synthetic branching', @derm), (@t2, N'Synthetic chain', @derm);
        INSERT INTO PathwayTemplateMaps (TemplateId, StageDefinitionId, StageOrder, ChoosesBranch, Branch)
        VALUES (@t1, @def, 1, 0, NULL), (@t1, @def, 2, 1, NULL), (@t1, @def, 3, 0, N'Benign'), (@t1, @def, 4, 0, N'Suspicious'),
               (@t1, @def, 5, 0, N'suspicious '), (@t1, @def, 6, 0, NULL),
               (@t2, @def, 1, 0, NULL), (@t2, @def, 2, 0, NULL);

        -- P1: open, branch point not chosen, nothing after it (grown); P2: branch chosen (Suspicious); P3: cancelled, not chosen;
        -- P4: open, not chosen, a visit booked after it (left as a chain).
        INSERT INTO PatientPathways (Id, Title, Notes, DueDateUtc, Priority, Status, PatientId, TeamId, TemplateId, TemplateName, CreatedAt, LastUpdated)
        VALUES ('b1000000-0000-0000-0000-000000000001', N'P1', N'', @now, 1, 3, @pt, @derm, @t1, N'Synthetic branching', @now, @now),
               ('b1000000-0000-0000-0000-000000000002', N'P2', N'', @now, 1, 3, @pt, @derm, @t1, N'Synthetic branching', @now, @now),
               ('b1000000-0000-0000-0000-000000000003', N'P3', N'', @now, 1, 6, @pt, @derm, @t1, N'Synthetic branching', @now, @now),
               ('b1000000-0000-0000-0000-000000000004', N'P4', N'', @now, 1, 3, @pt, @derm, @t1, N'Synthetic branching', @now, @now);

        INSERT INTO PatientPathwayStages (Id, PatientPathwayId, StageDefinitionId, StageOrder, Name, IsCompleted, ChoosesBranch, Branch, SkipReason, SkippedAtUtc, CreatedAt, LastUpdated)
        SELECT CAST(CONCAT('c', p.N, '000000-0000-0000-0000-00000000000', s.O) AS uniqueidentifier),
               CAST(CONCAT('b1000000-0000-0000-0000-00000000000', p.N) AS uniqueidentifier), @def, s.O, CONCAT(N'Stage ', s.O),
               CASE WHEN s.O = 1 OR (p.N = 2 AND s.O IN (2, 4)) THEN 1 ELSE 0 END,
               CASE WHEN s.O = 2 THEN 1 ELSE 0 END,
               CASE s.O WHEN 3 THEN N'Benign' WHEN 4 THEN N'Suspicious' WHEN 5 THEN N'Suspicious' END,
               CASE WHEN p.N = 2 AND s.O = 3 THEN 0 END,
               CASE WHEN p.N = 2 AND s.O = 3 THEN @now END,
               @now, @now
        FROM (VALUES (1), (2), (3), (4)) p(N) CROSS JOIN (VALUES (1), (2), (3), (4), (5), (6)) s(O);

        INSERT INTO PatientPathwayAuditEntries (Id, PatientPathwayId, PathwayTitle, PatientId, StageId, Action, UserId, Username, AtUtc, Details)
        VALUES ('a2000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000002', N'P2', @pt, 'c2000000-0000-0000-0000-000000000002', 36, @u4, N'synthetic.nurse', @now,
                N'{"branch":"Suspicious","skippedStageIds":[],"skippedStages":["Stage 3"]}'),
               ('a2000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000000', N'', @pt, NULL, 40, @u4, N'synthetic.nurse', @now,
                N'{"letterId":"a3000000-0000-0000-0000-000000000001"}');

        INSERT INTO Letters (Id, PatientId, TemplateName, Kind, Letterhead, Body, Closing, LargePrint, Status, AuthorUserId, Version, CreatedAt, LastUpdated)
        VALUES ('a3000000-0000-0000-0000-000000000001', @pt, N'Synthetic', 0, N'', N'', N'', 0, 0, @u4, 1, @now, @now);

        INSERT INTO Laboratories (Id, Name, UsesMedApp, IsArchived, CreatedAt, LastUpdated) VALUES ('a4000000-0000-0000-0000-000000000001', N'Synthetic lab', 1, 0, @now, @now);
        INSERT INTO SpecimenTypes (Id, Name, ExpectedTurnaroundDays, IsArchived, CreatedAt, LastUpdated) VALUES ('a5000000-0000-0000-0000-000000000001', N'Synthetic type', 7, 0, @now, @now);
        INSERT INTO Specimens (Id, Number, PatientId, PathwayId, StageId, SpecimenTypeId, LaboratoryId, RequestingUserId, Status, TakenAtUtc, TakenByUserId, RecordedAtUtc, Version)
        VALUES ('a6000000-0000-0000-0000-000000000001', 'SYN-000001', @pt, 'b1000000-0000-0000-0000-000000000002', 'c2000000-0000-0000-0000-000000000001',
                'a5000000-0000-0000-0000-000000000001', 'a4000000-0000-0000-0000-000000000001', @u4, 0, @now, @u4, @now, 1);

        INSERT INTO Appointments (Id, PatientId, PathwayId, StageId, Status, IsAutomatic, CreatedAtUtc, CreatedByUserId, LastUpdated)
        VALUES ('a7000000-0000-0000-0000-000000000001', @pt, 'b1000000-0000-0000-0000-000000000004', 'c4000000-0000-0000-0000-000000000004', 0, 0, @now, @u3, @now);
        INSERT INTO Appointments (Id, PatientId, StaffUserId, Status, IsAutomatic, CreatedAtUtc, CreatedByUserId, LastUpdated)
        VALUES ('a7000000-0000-0000-0000-000000000002', @pt, @u4, 0, 0, @now, @u1, @now);

        -- An MDT whose item is still listed: its attendee (the lab member) gets a grant.
        INSERT INTO Meetings (Id, Title, StartUtc, EndUtc, OrganiserUserId, IsMdt, TeamId, CreatedAtUtc, LastUpdated)
        VALUES ('a8000000-0000-0000-0000-000000000001', N'Synthetic MDT', @now, DATEADD(hour, 1, @now), @u4, 1, @derm, @now, @now);
        INSERT INTO MeetingAttendees (MeetingId, StaffUserId) VALUES ('a8000000-0000-0000-0000-000000000001', @u2);
        INSERT INTO MdtItems (Id, PatientId, PathwayId, TeamId, MeetingId, Question, Status, ListedByUserId, ListedByUsername, ListedAtUtc, Version)
        VALUES ('a9000000-0000-0000-0000-000000000001', @pt, 'b1000000-0000-0000-0000-000000000002', @derm, 'a8000000-0000-0000-0000-000000000001', N'', 0, @u4, N'synthetic.nurse', @now, 1);
        """;

    private MedAppDbContext _context = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_MultiClinicCentre_{Guid.NewGuid():N}"))
            .Options;
        _context = new MedAppDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(Before);
        await _context.Database.ExecuteSqlRawAsync(Seed);
    }

    [TearDown]
    public async Task DropDatabaseAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task Up_AddsTheLab_AndPutsEveryoneWithARoleInATeam_ButAdmins()
    {
        await MigrateAsync(Migration);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM Teams WHERE Id = {Lab} AND Name = N'Lab' AND Kind = 1")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Teams WHERE Id = {OldLab} AND Name = N'Lab (clinic)' AND Kind = 0")).Should().Be(1);
        (await TeamsOfAsync(Clinician)).Should().Equal(General);
        (await TeamsOfAsync(LabMember)).Should().Equal(Lab);
        (await TeamsOfAsync(AllStaff)).Should().Equal(General);
        (await TeamsOfAsync(Admin)).Should().Equal(Clinic);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM AspNetRoles r WHERE r.Name = N'Pathologist' AND (SELECT COUNT(*) FROM RolePermissions p WHERE p.RoleId = r.Id) = 7")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM RolePermissions WHERE Permission = N'AuthoriseLabReports'")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM AspNetRoles WHERE Name = N'All staff (before roles)' AND IsArchived = 1")).Should().Be(1);
    }

    [Test]
    public async Task Up_BackfillsEveryTeam_AndWritesTheGrants()
    {
        await MigrateAsync(Migration);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM Specimens WHERE Id = {Specimen} AND TeamId = {Clinic} AND RequestingTeamId = {Clinic} AND Source = 0")).Should().Be(1);
        // The visit on a pathway takes its team; the one without, its clinician's.
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Appointments WHERE TeamId = {Clinic}")).Should().Be(2);
        // The letter without a pathway takes its author's team, and its audit entry the letter's.
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Letters WHERE TeamId = {Clinic}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayAuditEntries WHERE TeamId <> {Clinic}")).Should().Be(0);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayAccess WHERE Reason = 0 AND TeamId = {Clinic} AND EndedAtUtc IS NULL")).Should().Be(4);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayAccess WHERE Reason = 1 AND TeamId = {Lab} AND PathwayId = {Chosen}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayAccess WHERE Reason = 3 AND UserId = {LabMember} AND MdtItemId = {MdtItem} AND PathwayId = {Chosen}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayAccess")).Should().Be(6);
    }

    [Test]
    public async Task Up_TurnsBranchPointsIntoChoiceNodes()
    {
        await MigrateAsync(Migration);

        var outcomes = JsonSerializer.Deserialize<List<PathwayTemplateOutcome>>(
            await TextAsync($"SELECT Outcomes AS Value FROM PathwayTemplateMaps WHERE TemplateId = {BranchingTemplate} AND StageOrder = 2 AND ChoiceKind = 0"), Json)!;
        outcomes.Select(o => (o.Name, string.Join(",", o.NextStageOrders))).Should().Equal(("Benign", "3"), ("Suspicious", "4"));
        var links = JsonSerializer.Deserialize<List<PathwayTemplateLink>>(
            await TextAsync($"SELECT Links AS Value FROM PathwayTemplates WHERE Id = {BranchingTemplate}"), Json)!;
        links.Should().Equal(Link(1, 2), Link(2, 3), Link(2, 4), Link(3, 6), Link(4, 5), Link(5, 6));
        (await TextAsync($"SELECT Links AS Value FROM PathwayTemplates WHERE Id = {ChainTemplate}")).Should().Be("[]");

        // Chosen: the outcome recorded, its stages linked as the template's.
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStages WHERE PatientPathwayId = {Chosen} AND StageOrder = 2 AND ChoiceKind = 0 AND Outcome = N'Suspicious'")).Should().Be(1);
        (await LinksOfAsync(Chosen)).Should().Equal("1-2", "2-3", "2-4", "3-6", "4-5", "5-6");

        // Still to choose with nothing after it: grown from its plan, up to the choice node.
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStages WHERE PatientPathwayId = {Grown}")).Should().Be(2);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStages WHERE PatientPathwayId = {Grown} AND StageOrder = 2 AND ChoiceKind = 0 AND Outcome IS NULL")).Should().Be(1);
        (await LinksOfAsync(Grown)).Should().Equal("1-2");
        var plan = JsonSerializer.Deserialize<PatientPathwayPlan>(await TextAsync($"SELECT [Plan] AS Value FROM PatientPathways WHERE Id = {Grown}"), Json)!;
        plan.Stages.Select(s => s.TemplateStageOrder).Should().Equal(1, 2, 3, 4, 5, 6);
        plan.Stages[1].ChoiceKind.Should().Be(StageChoiceKind.Assessment);
        plan.Stages[1].Outcomes.Select(o => o.Name).Should().Equal("Benign", "Suspicious");
        plan.Links.Should().HaveCount(6);

        // Cancelled, or with a visit booked after it: a plain stage, in a chain.
        foreach (var pathway in new[] { Cancelled, WorkAfter })
        {
            (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStages WHERE PatientPathwayId = {pathway} AND ChoiceKind IS NULL AND TemplateStageOrder = StageOrder")).Should().Be(6);
            (await LinksOfAsync(pathway)).Should().Equal("1-2", "2-3", "3-4", "4-5", "5-6");
        }
    }

    [Test]
    public async Task Down_PutsTheBranchesBack_AndUpAgainWorks()
    {
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration);

        await migrator.MigrateAsync(Before);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayTemplateMaps WHERE TemplateId = {BranchingTemplate} AND StageOrder = 2 AND ChoosesBranch = 1")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayTemplateMaps WHERE TemplateId = {BranchingTemplate} AND Branch = N'Suspicious'")).Should().Be(2);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStages WHERE PatientPathwayId = {Chosen} AND Branch = N'Benign'")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Specimens s JOIN Laboratories l ON l.Id = s.LaboratoryId WHERE s.Id = {Specimen}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name IN (N'PathwayAccess', N'LabCases', N'Hospitals', N'Consults')")).Should().Be(0);

        await migrator.MigrateAsync(Migration);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStages WHERE PatientPathwayId = {Chosen} AND Outcome = N'Suspicious'")).Should().Be(1);
    }

    private static PathwayTemplateLink Link(int from, int to) => new(from, to);

    private Task MigrateAsync(string migration) => _context.GetService<IMigrator>().MigrateAsync(migration);

    private async Task<List<Guid>> TeamsOfAsync(Guid userId) =>
        await _context.Database.SqlQuery<Guid>($"SELECT TeamId AS Value FROM TeamMemberships WHERE UserId = {userId}").ToListAsync();

    private async Task<List<string>> LinksOfAsync(Guid pathwayId) =>
        await _context.Database.SqlQuery<string>($"""
            SELECT CONCAT(f.StageOrder, '-', t.StageOrder) AS Value
            FROM PatientPathwayStageLinks l
            JOIN PatientPathwayStages f ON f.Id = l.FromStageId
            JOIN PatientPathwayStages t ON t.Id = l.ToStageId
            WHERE l.PatientPathwayId = {pathwayId}
            """).OrderBy(v => v).ToListAsync();

    private Task<string> TextAsync(FormattableString sql) => _context.Database.SqlQuery<string>(sql).SingleAsync();

    private Task<int> CountAsync(FormattableString sql) => _context.Database.SqlQuery<int>(sql).SingleAsync();
}
