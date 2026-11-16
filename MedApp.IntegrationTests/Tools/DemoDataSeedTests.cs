using System.Text.Json;
using FluentAssertions;
using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Patients.Queries.SearchRetentionReview;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Insights.Queries.GetProcedureInsights;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Appointments;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Tools;

/// <summary>
/// medapp-dev's demo data set (<see cref="DemoDataSeeder"/>), run in full on a fresh database of its own on the test
/// SQL Server, with the test Azurite for files, then checked against the rules the app enforces: visits before
/// finishing, no calendar clashes, bookings with whoever started the stage, Insights that differ for every period and
/// template, synthetic identifiers only, and the same counts from the same seed.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class DemoDataSeedTests
{
    private const string Database = "MedApp_DemoSeed";
    private const int Seed = 2026;

    private static readonly int[] Periods = [4, 12, 26];

    private IConfiguration _configuration = null!;
    private DateTime _now;
    private Dictionary<string, object?> _summary = null!;
    private SeedServices _services = null!;

    [OneTimeSetUp]
    public async Task SeedAsync()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestEnvironment.ServerConnectionString(Database),
                ["ConnectionStrings:Attachments"] = TestEnvironment.AttachmentsConnectionString,
                ["Identity:PasswordRequiredLength"] = "8",
                ["Identity:RequireDigit"] = "true",
                ["Identity:RequireLowercase"] = "true",
                ["Identity:RequireUppercase"] = "true",
                ["Identity:RequireNonAlphanumeric"] = "false",
                ["Identity:MaxFailedAccessAttempts"] = "5",
                ["Identity:LockoutMinutes"] = "5",
            })
            .Build();

        // A fixed "now", so the second run (determinism) replays the same story.
        _now = DateTime.UtcNow;
        _services = new SeedServices(_configuration, _now);
        await _services.Run(sp => sp.GetRequiredService<MedAppDbContext>().Database.MigrateAsync());

        _summary = await DemoDataSeeder.RunAsync(_configuration, new DemoSeedOptions(Seed, _now, []), CancellationToken.None);
    }

    [OneTimeTearDown]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    private Task<T> Query<T>(Func<MedAppDbContext, Task<T>> query) =>
        _services.Run(sp => query(sp.GetRequiredService<MedAppDbContext>()));

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

        requiredBypasses.Should().HaveCount(2, "the admin bypass is rare");
        requiredBypasses.Should().OnlyContain(b => b.Details.Contains("\"reason\":"));
    }

    [Test]
    public async Task Calendars_NoAppointmentOverlapsAnotherOrABlock_OrFallsOutsideTheWorkingDay()
    {
        var appointments = await Query(db => db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Attended || a.Status == AppointmentStatus.DidNotAttend)
            .ToListAsync());
        appointments.Should().HaveCountGreaterThan(1000);

        foreach (var group in appointments.GroupBy(a => (a.StaffUserId!.Value, Month: UkTime.DateOf(a.StartUtc!.Value).DayNumber / 28)))
        {
            var first = DateOnly.FromDayNumber(group.Key.Month * 28);
            var data = await _services.Run(sp => sp.GetRequiredService<ICalendarRepository>()
                .GetCalendarDataAsync(group.Key.Item1, first, first.AddDays(27), CancellationToken.None));

            foreach (var appointment in group)
            {
                var (start, end) = (appointment.StartUtc!.Value, appointment.EndUtc!.Value);
                WorkingHours.WhyOutside(start, end).Should().BeNull($"appointment {appointment.Id}");
                CalendarClashes.Find(data, start, end, ignoreAppointmentId: appointment.Id).Should().BeNull($"appointment {appointment.Id}");
            }
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

    [Test]
    public async Task Insights_DifferForEveryPeriodAndTemplate()
    {
        var templates = await Query(db => db.PatientProcedureStageTemplates.AsNoTracking().Select(t => (Guid?)t.Id).ToListAsync());
        templates.Should().HaveCount(6);

        var results = new Dictionary<string, string>();
        foreach (var weeks in Periods)
        foreach (var template in templates.Prepend(null))
        {
            _services.At(_now, new ActivityActor(null, "test", null));
            var response = await _services.Run(sp => sp.GetRequiredService<IMediator>().Send(new GetProcedureInsightsQuery(weeks, template)));
            results[$"{weeks} weeks, {template?.ToString() ?? "all"}"] = JsonSerializer.Serialize(response);
        }

        results.Values.Should().OnlyHaveUniqueItems("every period and template shows something different");

        // Within a period, the templates' charts differ too, not just their headlines.
        foreach (var weeks in Periods)
            templates.Select(t => results[$"{weeks} weeks, {t}"]).Select(StageTimesAndSeries)
                .Where(s => s != "[][]").Should().OnlyHaveUniqueItems($"the {weeks}-week charts");

        static string StageTimesAndSeries(string json)
        {
            using var document = JsonDocument.Parse(json);
            var parts = document.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                .Select(p => p.Value.GetRawText());
            return string.Concat(parts);
        }
    }

    [Test]
    public async Task Synthetic_NhsNumbersAreInThe999Range_AndEmailsOnReservedDomains()
    {
        var patients = await Query(db => db.Patients.AsNoTracking().ToListAsync());
        var users = await Query(db => db.Users.AsNoTracking().ToListAsync());

        patients.Should().HaveCountGreaterThan(200);
        patients.Where(p => p.NhsNumber != null).Should().OnlyContain(p => p.NhsNumber!.StartsWith("999") && NhsNumbers.IsValid(p.NhsNumber));
        patients.Where(p => p.AnonymisedAtUtc == null).Should().OnlyContain(p => p.Email.EndsWith("@example.com"));
        users.SelectMany(u => new[] { u.Email, u.PendingEmail }).OfType<string>().Should().OnlyContain(e => e.EndsWith("@clinic.example"));
    }

    [Test]
    public async Task Story_HasItsSpecialCases()
    {
        var (deletedUserWork, restricted, disputed, anonymised, lockedOut, mustChange, twoFactor) = await Query(async db => (
            await db.PatientProcedureAssignments.CountAsync(a => !db.Users.Any(u => u.Id == a.UserId)
                                                                  && a.PatientProcedure.Status != PatientProcedureStatus.Completed
                                                                  && a.PatientProcedure.Status != PatientProcedureStatus.Archived
                                                                  && a.PatientProcedure.Status != PatientProcedureStatus.Cancelled),
            await db.Patients.CountAsync(p => p.RestrictedAtUtc != null),
            await db.Patients.CountAsync(p => p.DisputedAtUtc != null),
            await db.Patients.CountAsync(p => p.AnonymisedAtUtc != null),
            await db.Users.CountAsync(u => u.LockoutEnd > DateTimeOffset.UtcNow),
            await db.Users.CountAsync(u => u.MustChangePassword),
            await db.Users.CountAsync(u => u.TwoFactorEnabled)));

        deletedUserWork.Should().Be(0, "no open work is left with a deleted account (H-21)");
        restricted.Should().Be(2);
        disputed.Should().Be(2);
        anonymised.Should().Be(1);
        lockedOut.Should().Be(1, "the healthcare assistant is locked out");
        mustChange.Should().Be(1, "the new starter must change their password");
        twoFactor.Should().Be(1);

        _services.At(_now, new ActivityActor(null, "test", null));
        var review = await _services.Run(sp => sp.GetRequiredService<IMediator>().Send(new SearchRetentionReviewQuery(1, 50, IncludeKept: true)));
        review.TotalCount.Should().Be(6, "six imported records are due, two of them kept");

        var files = JsonSerializer.Serialize(_summary["files"]);
        files.Should().Contain("\"uploaded\":").And.NotContain("skipped");
    }

    [Test]
    [Order(100)] // last: it seeds the database again
    public async Task SameSeed_GivesTheSameCounts()
    {
        var again = await DemoDataSeeder.RunAsync(_configuration, new DemoSeedOptions(Seed, _now, []), CancellationToken.None);

        JsonSerializer.Serialize(again["counts"]).Should().Be(JsonSerializer.Serialize(_summary["counts"]));
    }
}
