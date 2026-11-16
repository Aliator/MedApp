using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Patients.Queries.SearchRetentionReview;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Insights.Queries.GetProcedureInsights;
using MedApp.Contracts.Procedures.Insights.Responses;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Authentication;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Tools;

/// <summary>
/// medapp-dev's demo data set (<see cref="DemoDataSeeder"/>), run in full at a fixed moment on a fresh database of its
/// own on the test SQL Server, with the test Azurite for files, then checked against the rules the app enforces:
/// visits before finishing, no calendar clashes (appointments and stored blocks alike), bookings with whoever started
/// the stage, Insights that differ for every period and template, synthetic identifiers only, nothing seen before it
/// existed or dated in the future, and the story's special cases.
/// <para>
/// The story is told relative to "now", so every check runs at several moments (<see cref="DemoDataSeedTests"/> and
/// <see cref="DemoDataReseedTests"/>): fixed ones, so a failure repeats.
/// </para>
/// </summary>
public abstract class DemoDataSeedTestBase(string name, DateTime nowUtc)
{
    protected const int Seed = 2026;

    private static readonly int[] Periods = [4, 12, 26];

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
            $"Demo data at {UkTime.Describe(Now)} (UK) seeded in {timer.Elapsed.TotalSeconds:0} s: {JsonSerializer.Serialize(summary["secondsByPhase"])}; " +
            $"counts {JsonSerializer.Serialize(summary["counts"])}; files {JsonSerializer.Serialize(summary["files"])}");
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

    private JsonElement SummaryPart(string key) => JsonSerializer.SerializeToElement(Summary[key]);

    [Test]
    public async Task Visits_EveryFinishedStageThatNeedsOne_WasAttendedBeforeIt_OrAnAdminFinishedItWithoutOne()
    {
        var (stages, attended, bypassed) = await Query(async db => (
            await db.PatientProcedureStages.AsNoTracking()
                .Where(s => s.VisitRequirement == VisitRequirement.Required && s.IsCompleted).ToListAsync(),
            await db.Appointments.AsNoTracking()
                .Where(a => a.StageId != null && a.Status == AppointmentStatus.Attended).ToListAsync(),
            await db.PatientProcedureAuditEntries.AsNoTracking()
                .Where(a => a.Action == PatientProcedureAuditAction.StageFinished && a.Details.Contains("\"withoutVisit\":true"))
                .Select(a => new { a.StageId, a.Details }).ToListAsync()));

        stages.Should().NotBeEmpty();
        var firstFinish = await Query(db => db.PatientProcedureAuditEntries.AsNoTracking()
            .Where(a => a.Action == PatientProcedureAuditAction.StageFinished)
            .GroupBy(a => a.StageId).Select(g => new { StageId = g.Key, At = g.Min(a => a.AtUtc) })
            .ToDictionaryAsync(x => x.StageId!.Value, x => x.At));

        var requiredBypasses = bypassed.Where(b => b.Details.Contains("\"visitRequirement\":\"Required\"")).ToList();
        foreach (var stage in stages)
        {
            var visited = attended.Any(a => a.StageId == stage.Id && a.EndUtc <= firstFinish[stage.Id]);
            var bypass = requiredBypasses.Any(b => b.StageId == stage.Id);
            (visited || bypass).Should().BeTrue($"stage {stage.Id} ({stage.Name}) was finished");
        }

        requiredBypasses.Should().HaveCount(SummaryPart("counts").GetProperty("procedures").GetProperty("stageBypasses").GetInt32())
            .And.HaveCountGreaterThan(0).And.HaveCountLessThan(4, "the admin bypass is rare");
        requiredBypasses.Should().OnlyContain(b => b.Details.Contains("\"reason\":"));
    }

    /// <summary>
    /// Every appointment and every stored block, checked as the app checks a new one (<see cref="CalendarClashes"/>)
    /// against everything else on that person's calendar: appointments, stored blocks and the day templates' blocks.
    /// </summary>
    [Test]
    public async Task Calendars_NoAppointmentOrStoredBlockOverlapsAnythingElse_NorFallsOutsideTheWorkingDay()
    {
        var appointments = await Query(db => db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Attended || a.Status == AppointmentStatus.DidNotAttend)
            .ToListAsync());
        var blocks = await Query(db => db.CalendarBlocks.AsNoTracking().ToListAsync());
        appointments.Should().HaveCountGreaterThan(1000);
        blocks.Should().HaveCountGreaterThan(50);

        var starts = appointments.Select(a => a.StartUtc!.Value).Concat(blocks.Select(b => b.StartUtc)).ToList();
        var (from, to) = (UkTime.DateOf(starts.Min()), UkTime.DateOf(starts.Max()));
        var people = appointments.Select(a => a.StaffUserId!.Value).Concat(blocks.Select(b => b.StaffUserId)).Distinct().ToList();

        foreach (var person in people)
        {
            // One load per person; each check then projects only the date it's on.
            var data = await _services.Run(sp => sp.GetRequiredService<ICalendarRepository>()
                .GetCalendarDataAsync(person, from, to, CancellationToken.None));
            CalendarData On(DateTime startUtc) => data with { From = UkTime.DateOf(startUtc), To = UkTime.DateOf(startUtc) };

            foreach (var appointment in appointments.Where(a => a.StaffUserId == person))
            {
                var (start, end) = (appointment.StartUtc!.Value, appointment.EndUtc!.Value);
                WorkingHours.WhyOutside(start, end).Should().BeNull($"appointment {appointment.Id}");
                CalendarClashes.Find(On(start), start, end, ignoreAppointmentId: appointment.Id).Should().BeNull($"appointment {appointment.Id}");
            }

            foreach (var block in blocks.Where(b => b.StaffUserId == person))
                CalendarClashes.Find(On(block.StartUtc), block.StartUtc, block.EndUtc, ignoreBlockId: block.Id)
                    .Should().BeNull($"block {block.Id}, {UkTime.Describe(block.StartUtc)}");
        }
    }

    [Test]
    public async Task Bookings_ForCurrentStages_AreWithWhoeverStartedTheStage()
    {
        var procedures = await Query(db => db.PatientProcedures.AsNoTracking().Include(p => p.Stages)
            .Where(p => p.Status == PatientProcedureStatus.InProgress || p.Status == PatientProcedureStatus.Assigned)
            .ToListAsync());
        var booked = await Query(db => db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Booked && a.StageId != null).ToListAsync());

        var checkedOnes = 0;
        foreach (var procedure in procedures)
        {
            if (ProcedureWorkflow.CurrentStage(procedure) is not { } current)
                continue;
            foreach (var appointment in booked.Where(a => a.StageId == current.Id))
            {
                current.StartedByUserId.Should().Be(appointment.StaffUserId!.Value, $"appointment {appointment.Id} is for a stage under way");
                checkedOnes++;
            }
        }

        checkedOnes.Should().BeGreaterThan(10);
    }

    /// <summary>
    /// Each response repeats its question (the weeks, the template, the dates), so that's cleared before comparing:
    /// what must differ is the data.
    /// </summary>
    [Test]
    public async Task Insights_DifferForEveryPeriodAndTemplate()
    {
        var templates = await Query(db => db.PatientProcedureStageTemplates.AsNoTracking().Select(t => (Guid?)t.Id).ToListAsync());
        templates.Should().HaveCount(6);

        var results = new Dictionary<(int Weeks, Guid? Template), ProcedureInsightsResponse>();
        foreach (var weeks in Periods)
        foreach (var template in templates.Prepend(null))
        {
            _services.At(Now, new ActivityActor(null, "test", null));
            var response = await _services.Run(sp => sp.GetRequiredService<IMediator>().Send(new GetProcedureInsightsQuery(weeks, template)));
            results[(weeks, template)] = WithoutTheQuestion(response);
        }

        results.Values.Select(r => JsonSerializer.Serialize(r)).Should().OnlyHaveUniqueItems("every period and template shows something different");

        // Within a period, the templates' charts differ too: not just their headlines, and not just by their labels.
        foreach (var weeks in Periods)
            templates.Select(t => Charts(results[(weeks, t)])).Should().OnlyHaveUniqueItems($"the {weeks}-week charts");

        static ProcedureInsightsResponse WithoutTheQuestion(ProcedureInsightsResponse response)
        {
            response.Weeks = 0;
            response.TemplateId = null;
            response.From = default;
            response.To = default;
            foreach (var template in response.StageTimes)
            {
                template.TemplateId = null;
                template.TemplateName = string.Empty;
            }

            foreach (var week in response.Throughput)
                week.WeekStart = default;
            return response;
        }

        static string Charts(ProcedureInsightsResponse response) => JsonSerializer.Serialize(new
        {
            response.OverdueByAssignee,
            response.OverdueByPriority,
            StageTimes = response.StageTimes.SelectMany(t => t.Stages).Select(s => new { s.FinishedCount, s.AverageWaitingHours, s.AverageWorkingHours }),
            response.Throughput,
        });
    }

    [Test]
    public async Task Synthetic_NhsNumbersAreInThe999Range_EmailsOnReservedDomains_AndNamesUnique()
    {
        var patients = await Query(db => db.Patients.AsNoTracking().ToListAsync());
        var users = await Query(db => db.Users.AsNoTracking().ToListAsync());

        patients.Should().HaveCountGreaterThan(200);
        patients.Where(p => p.NhsNumber != null).Should().OnlyContain(p => p.NhsNumber!.StartsWith("999") && NhsNumbers.IsValid(p.NhsNumber));
        patients.Where(p => p.AnonymisedAtUtc == null).Should().OnlyContain(p => p.Email.EndsWith("@example.com"));
        users.SelectMany(u => new[] { u.Email, u.PendingEmail }).OfType<string>().Should().OnlyContain(e => e.EndsWith("@clinic.example"));

        patients.Where(p => p.AnonymisedAtUtc == null).Select(p => $"{p.FirstName} {p.LastName}")
            .Should().OnlyHaveUniqueItems("the summary and the tour name records, so a name must find one record");
    }

    /// <summary>The records the summary names for the tour can each be found by name, and are what it says they are.</summary>
    [Test]
    public async Task Summary_NamesEachRecordItPointsTo_Unambiguously()
    {
        var patients = await Query(db => db.Patients.AsNoTracking().ToListAsync());
        var privacy = SummaryPart("privacy");
        List<Patient> Named(string list) => privacy.GetProperty(list).EnumerateArray()
            .Select(name => patients.Should().ContainSingle(p => $"{p.FirstName} {p.LastName}" == name.GetString(), $"{list} names {name}").Subject)
            .ToList();

        Named("restricted").Should().HaveCount(2).And.OnlyContain(p => p.RestrictedAtUtc != null);
        Named("disputed").Should().HaveCount(2).And.OnlyContain(p => p.DisputedAtUtc != null);
        Named("retentionKept").Should().HaveCount(2).And.OnlyContain(p => p.RetentionKeptUntil != null);
        Named("retentionDue").Should().HaveCount(6).And.OnlyContain(p => p.AnonymisedAtUtc == null);
    }

    [Test]
    public async Task Story_HasItsSpecialCases()
    {
        var nowOffset = new DateTimeOffset(Now);
        var (deletedUserWork, restricted, disputed, anonymised, lockedOut, mustChange, twoFactor) = await Query(async db => (
            await db.PatientProcedureAssignments.CountAsync(a => !db.Users.Any(u => u.Id == a.UserId)
                                                                  && a.PatientProcedure.Status != PatientProcedureStatus.Completed
                                                                  && a.PatientProcedure.Status != PatientProcedureStatus.Archived
                                                                  && a.PatientProcedure.Status != PatientProcedureStatus.Cancelled),
            await db.Patients.CountAsync(p => p.RestrictedAtUtc != null),
            await db.Patients.CountAsync(p => p.DisputedAtUtc != null),
            await db.Patients.CountAsync(p => p.AnonymisedAtUtc != null),
            await db.Users.Where(u => u.LockoutEnd > nowOffset).Select(u => u.UserName!).ToListAsync(),
            await db.Users.CountAsync(u => u.MustChangePassword),
            await db.Users.CountAsync(u => u.TwoFactorEnabled)));

        deletedUserWork.Should().Be(0, "no open work is left with a deleted account (H-21)");
        restricted.Should().Be(2);
        disputed.Should().Be(2);
        anonymised.Should().Be(1);
        lockedOut.Should().Equal([DemoStaff.LockedUsername], "the healthcare assistant is locked out, whatever the day");
        mustChange.Should().Be(1, "the new starter must change their password");
        twoFactor.Should().Be(1);

        _services.At(Now, new ActivityActor(null, "test", null));
        var review = await _services.Run(sp => sp.GetRequiredService<IMediator>().Send(new SearchRetentionReviewQuery(1, 50, IncludeKept: true)));
        review.TotalCount.Should().Be(6, "six imported records are due, two of them kept");

        var files = JsonSerializer.Serialize(Summary["files"]);
        if (AttachmentsConnectionString is null)
            files.Should().Contain("\"skipped\":", "with no attachment store, files are skipped and the summary says so");
        else
            files.Should().Contain("\"uploaded\":").And.NotContain("skipped");
    }

    /// <summary>
    /// Today's sign-ins happen only on a working day, from 07:30: the summary says who signed in, and they're the ones
    /// with sessions. The lockout happens on any day.
    /// </summary>
    [Test]
    public async Task Today_SignInsMatchTheSummary_AndHappenOnlyOnWorkingDaysFromHalfPastSeven()
    {
        var today = SummaryPart("today");
        var signedIn = today.GetProperty("signedIn").EnumerateArray().Select(e => e.GetString()!).ToList();
        var withSessions = await Query(db => db.UserSessions.AsNoTracking()
            .Join(db.Users, s => s.UserId, u => u.Id, (_, u) => u.UserName!).Distinct().ToListAsync());

        withSessions.Should().BeEquivalentTo(signedIn);
        today.GetProperty("lockedOut").GetString().Should().Be(DemoStaff.LockedUsername);

        var time = UkTime.TimeOf(Now);
        if (!WorkingHours.IsWorkingDay(UkTime.DateOf(Now)) || time < new TimeOnly(7, 30))
            signedIn.Should().BeEmpty("nobody signs in at a weekend, or before 07:30");
        else if (time >= new TimeOnly(9, 0))
            signedIn.Should().HaveCountGreaterThan(5, "most staff have signed in by mid-morning on a working day");
    }

    /// <summary>
    /// Nothing in a patient's history comes before they were registered, nobody sees an anonymised record afterwards,
    /// and nothing is dated after "now".
    /// </summary>
    [Test]
    public async Task History_RespectsWhenEachPatientExisted_AndNothingIsInTheFuture()
    {
        var patients = await Query(db => db.Patients.AsNoTracking()
            .Select(p => new { p.Id, p.CreatedAt, p.AnonymisedAtUtc }).ToDictionaryAsync(p => p.Id));
        var views = new[] { ActivityAction.PatientViewed, ActivityAction.PatientListed, ActivityAction.ProcedureViewed };

        var activity = await Query(db => db.ActivityEntries.AsNoTracking()
            .Where(a => a.PatientId != null).Select(a => new { a.Id, PatientId = a.PatientId!.Value, a.AtUtc, a.Action }).ToListAsync());
        var aboutPatients = activity.Where(a => patients.ContainsKey(a.PatientId)).ToList();
        aboutPatients.Should().HaveCountGreaterThan(1000);
        aboutPatients.Where(a => a.AtUtc < patients[a.PatientId].CreatedAt)
            .Should().BeEmpty("nobody sees, lists or changes a patient before they're registered");
        aboutPatients.Where(a => views.Contains(a.Action) && patients[a.PatientId].AnonymisedAtUtc is { } anonymisedAt && a.AtUtc >= anonymisedAt)
            .Should().BeEmpty("an anonymised record is no longer seen or listed (it left the retention review)");

        var appointments = await Query(db => db.Appointments.AsNoTracking().ToListAsync());
        appointments.Where(a => a.CreatedAtUtc < patients[a.PatientId].CreatedAt || a.StartUtc < patients[a.PatientId].CreatedAt)
            .Should().BeEmpty("nobody is booked before they're registered");

        var audit = await Query(db => db.PatientProcedureAuditEntries.AsNoTracking().Select(a => new { a.Id, a.PatientId, a.AtUtc }).ToListAsync());
        audit.Where(a => patients.ContainsKey(a.PatientId) && a.AtUtc < patients[a.PatientId].CreatedAt).Should().BeEmpty();

        // Nothing in the future.
        audit.Where(a => a.AtUtc > Now).Should().BeEmpty();
        (await Query(db => db.ActivityEntries.CountAsync(a => a.AtUtc > Now))).Should().Be(0);
        (await Query(db => db.AppointmentEvents.CountAsync(e => e.AtUtc > Now))).Should().Be(0);
        (await Query(db => db.Notifications.CountAsync(n => n.CreatedAtUtc > Now))).Should().Be(0);
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
        // A Saturday morning: the diary shown is Monday's, nobody signs in, but the lockout still happens.
        new TestFixtureData("Saturday", new DateTime(2026, 11, 14, 11, 0, 0, DateTimeKind.Utc))
            .SetArgDisplayNames("Saturday 14 November 2026, 11:00"),
        // 00:20 on the Monday after the clocks went back (Sunday 25 October): a working day nobody has started yet.
        new TestFixtureData("AfterClockChange", new DateTime(2026, 10, 26, 0, 20, 0, DateTimeKind.Utc))
            .SetArgDisplayNames("Monday 26 October 2026, 00:20"),
    ];
}

/// <summary>
/// A weekday afternoon, seeded twice on the same database: the second seeding wipes the first's full data set, and the
/// same seed gives the same counts. Every check in the base runs against the second.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class DemoDataReseedTests() : DemoDataSeedTestBase("Friday", FridayAfternoon)
{
    /// <summary>16:30 on a Friday: a full working day behind it (and the moment the nurse's freed slot once clashed).</summary>
    private static readonly DateTime FridayAfternoon = new(2026, 11, 20, 16, 30, 0, DateTimeKind.Utc);

    protected override bool SeedsTwice => true;

    [Test]
    public void SameSeed_GivesTheSameCounts() =>
        JsonSerializer.Serialize(Summary["counts"]).Should().Be(JsonSerializer.Serialize(FirstSummary!["counts"]));
}
