using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Appointments;
using MedApp.Domain.Authentication;
using MedApp.Domain.Lab;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Tools;

/// <summary>
/// The demo data set (<see cref="DemoDataSeeder"/>, which tools/seed-demo-data.sh and medapp-dev both run): the light
/// demo's clinic, run in full at a fixed moment on a fresh database of its own on the test SQL Server, with the test
/// Azurite for files, then checked against the rules the app enforces and the points the light demo's records are at:
/// visits before finishing, no calendar clashes, clinical work only by staff in their teams (never the admin), the
/// pathways at their stages, the lab's cases and the hospital's requests, synthetic identifiers only, and nothing in
/// the future.
/// <para>
/// The story is told relative to "now", so every check runs at several moments (<see cref="DemoDataSeedTests"/> and
/// <see cref="DemoDataReseedTests"/>): fixed ones, so a failure repeats.
/// </para>
/// </summary>
public abstract class DemoDataSeedTestBase(string name, DateTime nowUtc)
{
    protected const int Seed = 2026;

    private readonly string _database = $"MedApp_DemoSeed_{name}";
    private IConfiguration _configuration = null!;
    private SeedServices _services = null!;

    /// <summary>The moment the story ends at: the seed's "now".</summary>
    protected DateTime Now { get; } = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

    /// <summary>The summary of the seeding the database holds (the last one).</summary>
    protected Dictionary<string, object?> Summary { get; private set; } = null!;

    /// <summary>The first seeding's summary, when the database was seeded twice.</summary>
    protected Dictionary<string, object?>? FirstSummary { get; private set; }

    /// <summary>Whether to seed twice, the second time over the first's data (the wipe of a full database).</summary>
    protected virtual bool SeedsTwice => false;

    /// <summary>A database on the server it seeds on: the test SQL Server.</summary>
    protected virtual string ConnectionString(string database) => TestEnvironment.ServerConnectionString(database);

    /// <summary>The attachment store files go to (the test Azurite); with none, the seed skips files and says so.</summary>
    protected virtual string? AttachmentsConnectionString => TestEnvironment.AttachmentsConnectionString;

    [OneTimeSetUp]
    public async Task SeedAsync()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString(_database),
                ["ConnectionStrings:Attachments"] = AttachmentsConnectionString,
                ["Identity:PasswordRequiredLength"] = "8",
                ["Identity:RequireDigit"] = "true",
                ["Identity:RequireLowercase"] = "true",
                ["Identity:RequireUppercase"] = "true",
                ["Identity:RequireNonAlphanumeric"] = "false",
                ["Identity:MaxFailedAccessAttempts"] = "5",
                ["Identity:LockoutMinutes"] = "5",
            })
            .Build();

        _services = new SeedServices(_configuration, Now);
        await _services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            await db.Database.MigrateAsync();
            // A rule an admin set before the reset, which the reset must put back to the default (off).
            db.SecuritySettings.Add(new SecuritySettings { RequireTwoFactorForAdmins = true, UpdatedAtUtc = Now.AddDays(-400) });
            await db.SaveChangesAsync();
        });

        if (SeedsTwice)
            FirstSummary = await SeedOnceAsync();
        Summary = await SeedOnceAsync();
    }

    private async Task<Dictionary<string, object?>> SeedOnceAsync()
    {
        var timer = Stopwatch.StartNew();
        var summary = await DemoDataSeeder.RunAsync(_configuration, new DemoSeedOptions(Seed, Now, []), CancellationToken.None);
        TestContext.Progress.WriteLine(
            $"Demo data at {UkTime.Describe(Now)} (UK) seeded in {timer.Elapsed.TotalSeconds:0} s: counts {JsonSerializer.Serialize(summary["counts"])}");
        return summary;
    }

    [OneTimeTearDown]
    public async Task TearDownAsync()
    {
        await _services.DisposeAsync();

        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(ConnectionString("master"));
        await connection.OpenAsync();
        await using var drop = connection.CreateCommand();
        drop.CommandText =
            $"IF DB_ID('{_database}') IS NOT NULL BEGIN ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]; END";
        await drop.ExecuteNonQueryAsync();
    }

    private Task<T> Query<T>(Func<MedAppDbContext, Task<T>> query) =>
        _services.Run(sp => query(sp.GetRequiredService<MedAppDbContext>()));

    private Task<Guid> UserIdAsync(string username) =>
        Query(db => db.Users.Where(u => u.UserName == username).Select(u => u.Id).SingleAsync());

    /// <summary>A pathway's stages in order: each one's name, whether it's finished, started or neither, and its outcome.</summary>
    private Task<List<string>> StagesOf(string firstName, string lastName, string title) =>
        Query(async db =>
        {
            var patientId = await db.Patients.Where(p => p.FirstName == firstName && p.LastName == lastName).Select(p => p.Id).SingleAsync();
            var pathway = await db.PatientPathways.AsNoTracking().Include(p => p.Stages)
                .SingleAsync(p => p.PatientId == patientId && p.Title == title);
            return pathway.Stages.OrderBy(s => s.StageOrder)
                .Select(s => $"{s.Name}:{(s.IsCompleted ? "done" : s.StartedAtUtc is not null ? "started" : "-")}{(s.Outcome is null ? string.Empty : $"={s.Outcome}")}")
                .ToList();
        });

    [Test]
    public async Task Visits_EveryFinishedStageThatNeedsOne_WasAttendedBeforeIt()
    {
        var (stages, attended, finishes) = await Query(async db => (
            await db.PatientPathwayStages.AsNoTracking().Where(s => s.VisitRequirement == VisitRequirement.Required && s.IsCompleted).ToListAsync(),
            await db.Appointments.AsNoTracking().Where(a => a.StageId != null && a.Status == AppointmentStatus.Attended).ToListAsync(),
            await db.PatientPathwayAuditEntries.AsNoTracking()
                .Where(a => a.Action == PatientPathwayAuditAction.StageFinished)
                .GroupBy(a => a.StageId).Select(g => new { StageId = g.Key, At = g.Min(a => a.AtUtc) })
                .ToDictionaryAsync(x => x.StageId!.Value, x => x.At)));

        stages.Should().NotBeEmpty();
        foreach (var stage in stages)
            attended.Should().Contain(a => a.StageId == stage.Id && a.EndUtc <= finishes[stage.Id], $"stage {stage.Name} was finished");
    }

    /// <summary>Every appointment and stored block, checked as the app checks a new one against the rest of that person's calendar.</summary>
    [Test]
    public async Task Calendars_NoAppointmentOrStoredBlockOverlapsAnythingElse_NorFallsOutsideTheWorkingDay()
    {
        var appointments = await Query(db => db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Attended).ToListAsync());
        var blocks = await Query(db => db.CalendarBlocks.AsNoTracking().ToListAsync());
        appointments.Should().NotBeEmpty();
        blocks.Should().NotBeEmpty();

        var starts = appointments.Select(a => a.StartUtc!.Value).Concat(blocks.Select(b => b.StartUtc)).ToList();
        var (from, to) = (UkTime.DateOf(starts.Min()), UkTime.DateOf(starts.Max()));
        foreach (var person in appointments.Select(a => a.StaffUserId!.Value).Concat(blocks.Select(b => b.StaffUserId)).Distinct())
        {
            var data = await _services.Run(sp => sp.GetRequiredService<ICalendarRepository>().GetCalendarDataAsync(person, from, to, CancellationToken.None));
            CalendarData On(DateTime startUtc) => data with { From = UkTime.DateOf(startUtc), To = UkTime.DateOf(startUtc) };

            foreach (var appointment in appointments.Where(a => a.StaffUserId == person))
                CalendarClashes.Find(On(appointment.StartUtc!.Value), appointment.StartUtc!.Value, appointment.EndUtc!.Value,
                    ignoreAppointmentId: appointment.Id, ignoreSessionId: appointment.ClinicSessionId).Should().BeNull($"appointment {appointment.Id}");
            foreach (var block in blocks.Where(b => b.StaffUserId == person))
                CalendarClashes.Find(On(block.StartUtc), block.StartUtc, block.EndUtc, ignoreBlockId: block.Id).Should().BeNull($"block {block.Id}");
        }
    }

    /// <summary>Admins are read only: every pathway's history was made by staff, the lab's stages finished by the lab.</summary>
    [Test]
    public async Task ClinicalWork_IsDoneByStaffInTheirTeams_NeverTheAdmin()
    {
        var admin = await UserIdAsync("admin");
        var lab = await UserIdAsync("r.aldous");
        var (auditors, labStagesBy) = await Query(async db => (
            await db.PatientPathwayAuditEntries.Select(a => a.UserId).Distinct().ToListAsync(),
            await db.PatientPathwayStages.Where(s => s.OwnerTeamId == Team.LabId && s.CompletedByUserId != null)
                .Select(s => s.CompletedByUserId!.Value).Distinct().ToListAsync()));

        auditors.Should().NotContain(admin);
        labStagesBy.Should().Equal(lab);
    }

    [Test]
    public async Task Pathways_AreAtTheLightDemosPoints()
    {
        (await StagesOf("Ellis", "Varden", "Scaly lesion, left forearm")).Take(3).Should().Equal(
            "Triage:done", "Dermoscopy assessment:done=Uncertain: biopsy", "Take biopsy:-");
        (await StagesOf("Priya", "Holloway", "Non-pigmented lesion, upper back"))[3].Should().Be("Histology:started");
        (await StagesOf("Harriet", "Quill", "Basal cell carcinoma, left temple")).Should().Equal(
            "Triage:done", "Dermoscopy assessment:done=Uncertain: biopsy", "Take biopsy:done", "Histology:done",
            "Histology result:done=Low-risk BCC", "Refer to hospital:-");
        (await StagesOf("Callum", "Ashby", "Scaly patch, back of the right hand"))[4].Should().Be("Histology result:-");
        (await StagesOf("Felix", "Moreton", "Suspicious lesion, right shin")).Should().Equal("Triage:done", "Dermoscopy assessment:started");
        (await StagesOf("Leah", "Corrin", "Scaly patch, left forearm")).Should().Equal(
            "Triage:started", "Testing:-", "Results appointment:-", "Refer to GP for medication:-");
        (await StagesOf("Aisha", "Khoury", "Two lesions, left shin")).Should().HaveCount(7, "the second lesion's biopsy and histology were added");
        (await StagesOf("Tobias", "Lund", "New lesion, right cheek"))[0].Should().Be("Triage:-");

        var statuses = await Query(db => db.PatientPathways.Select(p => p.Status).ToListAsync());
        statuses.Should().HaveCount(16).And.Contain(PatientPathwayStatus.Blocked).And.Contain(PatientPathwayStatus.Unassigned);
    }

    [Test]
    public async Task Referrals_ThreeAwaitTriage_AndEllisVardensWasAccepted()
    {
        var referrals = await Query(db => db.Referrals.AsNoTracking().ToListAsync());

        referrals.Should().HaveCount(4);
        referrals.Count(r => r.Status == ReferralStatus.AwaitingTriage).Should().Be(3);
        referrals.Single(r => r.Status == ReferralStatus.Accepted).PathwayId.Should().NotBeNull();
    }

    [Test]
    public async Task Lab_CasesAndSamples_AreAtTheirSteps()
    {
        var cases = await Query(db => db.LabCases.AsNoTracking().ToListAsync());
        var samples = await Query(db => db.Specimens.AsNoTracking().ToListAsync());
        var results = await Query(db => db.Results.AsNoTracking().ToListAsync());

        cases.Count(c => c.Source == SpecimenSource.Hospital).Should().Be(5);
        cases.Should().Contain(c => c.Status == LabCaseStatus.RepeatRequested, "Aisha Khoury's second biopsy was too small");
        cases.Should().Contain(c => c.Status == LabCaseStatus.AwaitingSurgeryDetails);
        cases.Should().Contain(c => c.Status == LabCaseStatus.Closed);
        cases.Should().Contain(c => c.Status == LabCaseStatus.Open && c.LabTestId == null, "a lab stage's case waits for its sample");
        samples.Should().Contain(s => s.Status == SpecimenStatus.Taken).And.Contain(s => s.Status == SpecimenStatus.Sent);
        results.Count(r => r.Status == ResultStatus.AwaitingSignOff).Should().Be(2);
        results.Count(r => r.Status == ResultStatus.SignedOff).Should().Be(1);
    }

    [Test]
    public async Task Mdt_AnAttendeeFromAnotherTeam_ReadsTheListedPathways()
    {
        var lab = await UserIdAsync("r.aldous");
        var grants = await Query(db => db.PathwayAccess.AsNoTracking().Where(a => a.Reason == PathwayAccessReason.Mdt).ToListAsync());

        grants.Should().Contain(g => g.UserId == lab, "the lab's pathologist attends and presents at the Dermatology MDT");
    }

    [Test]
    public async Task Synthetic_NhsNumbersAreInThe999Range_AndEmailsOnReservedDomains()
    {
        var patients = await Query(db => db.Patients.AsNoTracking().ToListAsync());
        var emails = await Query(db => db.Users.Where(u => u.Email != null).Select(u => u.Email!).ToListAsync());

        patients.Should().HaveCount(19);
        patients.Should().OnlyContain(p => p.NhsNumber != null && p.NhsNumber.StartsWith("999") && NhsNumbers.IsValid(p.NhsNumber));
        // A hospital-only patient has no address (an empty one).
        patients.Where(p => !string.IsNullOrEmpty(p.Email)).Should().OnlyContain(p => p.Email!.EndsWith("@example.com"));
        emails.Should().OnlyContain(e => e.EndsWith(".example") || e.EndsWith("@example.com"));
    }

    [Test]
    public async Task History_HasNothingInTheFuture()
    {
        var (audit, activity, notifications) = await Query(async db => (
            await db.PatientPathwayAuditEntries.MaxAsync(a => a.AtUtc),
            await db.ActivityEntries.MaxAsync(a => a.AtUtc),
            await db.Notifications.MaxAsync(n => n.CreatedAtUtc)));

        audit.Should().BeOnOrBefore(Now);
        activity.Should().BeOnOrBefore(Now);
        notifications.Should().BeOnOrBefore(Now);
    }

    [Test]
    public async Task Wipe_PutsTheAdminsSignInRulesBackToTheDefaults() =>
        (await Query(db => db.SecuritySettings.AnyAsync())).Should().BeFalse("a new database has no rules row, so every rule is off");
}

/// <summary>The demo data at moments that change its shape: a weekend, and just after midnight the day after a clock change.</summary>
[TestFixtureSource(nameof(Moments))]
[NonParallelizable]
public sealed class DemoDataSeedTests(string name, DateTime nowUtc) : DemoDataSeedTestBase(name, nowUtc)
{
    public static IEnumerable<TestFixtureData> Moments =>
    [
        new TestFixtureData("Saturday", new DateTime(2026, 11, 14, 11, 0, 0, DateTimeKind.Utc))
            .SetArgDisplayNames("Saturday 14 November 2026, 11:00"),
        // 00:20 on the Monday after the clocks went back (Sunday 25 October): a working day nobody has started yet.
        new TestFixtureData("AfterClockChange", new DateTime(2026, 10, 26, 0, 20, 0, DateTimeKind.Utc))
            .SetArgDisplayNames("Monday 26 October 2026, 00:20"),
    ];
}

/// <summary>
/// A weekday afternoon, seeded twice on the same database: the second seeding wipes the first's full data set, and the
/// story gives the same counts. Every check in the base runs against the second.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class DemoDataReseedTests() : DemoDataSeedTestBase("Friday", FridayAfternoon)
{
    /// <summary>16:30 on a Friday: a full working day behind it.</summary>
    private static readonly DateTime FridayAfternoon = new(2026, 11, 20, 16, 30, 0, DateTimeKind.Utc);

    protected override bool SeedsTwice => true;

    [Test]
    public void SameStory_GivesTheSameCounts() =>
        JsonSerializer.Serialize(Summary["counts"]).Should().Be(JsonSerializer.Serialize(FirstSummary!["counts"]));
}
