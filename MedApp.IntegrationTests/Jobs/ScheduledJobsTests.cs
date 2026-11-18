using System.Net;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Jobs.OperationalPurge;
using MedApp.Contracts.Jobs.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Authentication;
using MedApp.Domain.Jobs;
using MedApp.Domain.Notepads;
using MedApp.Domain.Notifications;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Data.Repositories;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Jobs;

/// <summary>
/// The scheduled job runner against SQL Server: the lease that lets exactly one runner (one copy of the API) run a job,
/// the run history and the failure notice, the Jobs endpoints, and the operational purge's deletes.
/// </summary>
[TestFixture]
public sealed class ScheduledJobsTests : IntegrationTest
{
    /// <summary>A job that counts its runs, and takes a moment, so two runners racing for it overlap.</summary>
    private sealed class CountingJob : IScheduledJob
    {
        public const string JobName = "it-counting";
        public static int Runs;

        public string Name => JobName;
        public string Title => "Integration test counter";
        public string Description => "Counts its runs.";
        public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromHours(1));

        public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Runs);
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            return 1;
        }
    }

    /// <summary>A job that always fails.</summary>
    private sealed class FailingJob : IScheduledJob
    {
        public const string JobName = "it-failing";

        public string Name => JobName;
        public string Title => "Integration test failure";
        public string Description => "Always fails.";
        public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromHours(1));

        public Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synthetic failure with a message that mustn't be stored.");
    }

    /// <summary>An API with the two test jobs as well as the real ones (and these settings).</summary>
    private static WebApplicationFactory<Program> ApiWithTestJobs(params (string Key, string Value)[] settings) =>
        Api.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IScheduledJob, CountingJob>();
                services.AddScoped<IScheduledJob, FailingJob>();
            });
        });

    /// <summary>A runner of its own, as another copy of the API has: its own holder name in the lease.</summary>
    private static ScheduledJobRunner NewRunner(WebApplicationFactory<Program> api) =>
        ActivatorUtilities.CreateInstance<ScheduledJobRunner>(api.Services);

    private static Task<ScheduledJob?> GetRowAsync(string name) =>
        RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().ScheduledJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(job => job.Name == name));

    [SetUp]
    public void ResetCounter() => CountingJob.Runs = 0;

    [Test]
    public async Task TwoRunners_SameDueJobAtOnce_RunItOnce()
    {
        await using var api = ApiWithTestJobs();
        var first = NewRunner(api);
        var second = NewRunner(api);
        first.Holder.Should().NotBe(second.Holder);

        for (var round = 1; round <= 5; round++)
        {
            // Due now (a new job is due at once; later rounds put it back), and both runners go for it together.
            await ExecuteSqlAsync($"UPDATE [ScheduledJobs] SET [NextRunUtc] = {Now.AddMinutes(-1)} WHERE [Name] = {CountingJob.JobName}");
            await Task.WhenAll(
                Task.Run(() => first.RunDueAsync(CancellationToken.None)),
                Task.Run(() => second.RunDueAsync(CancellationToken.None)));

            CountingJob.Runs.Should().Be(round, "only the runner holding the lease runs the job");
        }

        var row = await GetRowAsync(CountingJob.JobName);
        row!.LastOutcome.Should().Be(ScheduledJobOutcome.Succeeded);
        row.LastCount.Should().Be(1);
        row.LeaseHolder.Should().BeNull();
        row.LeaseUntilUtc.Should().BeNull();
        row.NextRunUtc.Should().BeAfter(Now.AddMinutes(50));
    }

    [Test]
    public async Task TwoRunners_RunNowAtOnce_OneRunsAndTheOtherIsRefused()
    {
        await using var api = ApiWithTestJobs();
        var first = NewRunner(api);
        var second = NewRunner(api);

        var results = await Task.WhenAll(
            Task.Run(() => Attempt(first)),
            Task.Run(() => Attempt(second)));

        CountingJob.Runs.Should().Be(1);
        results.Should().ContainSingle(ran => ran);

        static async Task<bool> Attempt(ScheduledJobRunner runner)
        {
            try
            {
                await runner.RunNowAsync(CountingJob.JobName, CancellationToken.None);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }
    }

    [Test]
    public async Task Runner_LeaseHeldByAnother_WaitsUntilItEnds()
    {
        await using var api = ApiWithTestJobs();
        var runner = NewRunner(api);
        await runner.RunNowAsync(CountingJob.JobName, CancellationToken.None);
        CountingJob.Runs = 0;

        // Another copy holds it (and it's due): this runner leaves it alone, and Run now is refused.
        await ExecuteSqlAsync(
            $"UPDATE [ScheduledJobs] SET [NextRunUtc] = {Now.AddMinutes(-1)}, [LeaseHolder] = 'another-copy', [LeaseUntilUtc] = {Now.AddMinutes(5)} WHERE [Name] = {CountingJob.JobName}");
        await runner.RunDueAsync(CancellationToken.None);
        var runNow = () => runner.RunNowAsync(CountingJob.JobName, CancellationToken.None);
        await runNow.Should().ThrowAsync<ConflictException>();
        CountingJob.Runs.Should().Be(0);

        // That copy died: once its lease ends, this runner takes the job.
        await ExecuteSqlAsync(
            $"UPDATE [ScheduledJobs] SET [LeaseUntilUtc] = {Now.AddSeconds(-1)} WHERE [Name] = {CountingJob.JobName}");
        await runner.RunDueAsync(CancellationToken.None);

        CountingJob.Runs.Should().Be(1);
        (await GetRowAsync(CountingJob.JobName))!.LeaseHolder.Should().BeNull();
    }

    [Test]
    public async Task Runner_FailsThreeTimesInARow_TellsTheAdminsOnceAndKeepsOnlyTheErrorType()
    {
        await using var api = ApiWithTestJobs();
        var runner = NewRunner(api);

        for (var i = 0; i < 4; i++)
            await runner.RunNowAsync(FailingJob.JobName, CancellationToken.None);

        var row = await GetRowAsync(FailingJob.JobName);
        row!.LastOutcome.Should().Be(ScheduledJobOutcome.Failed);
        row.ConsecutiveFailures.Should().Be(4);
        row.LastErrorType.Should().Be(nameof(InvalidOperationException));
        row.LastCount.Should().BeNull();
        // A failure is tried again sooner than its interval.
        row.NextRunUtc.Should().BeOnOrBefore(Now.Add(JobSchedule.LongestRetryDelay).AddMinutes(1));

        var admin = await SignInAsAdminAsync();
        var notifications = await (await admin.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
        notifications.Items.Where(n => n.Kind == nameof(NotificationKind.ScheduledJobFailing)).Should().ContainSingle()
            .Which.Message.Should().NotContain("Synthetic failure");
    }

    [Test]
    public async Task GetJobs_Admin_ListsTheOperationalPurge()
    {
        var admin = await SignInAsAdminAsync();

        var jobs = await (await admin.GetAsync("/api/jobs")).ReadAsAsync<List<ScheduledJobResponse>>();

        var purge = jobs.Should().ContainSingle(job => job.Name == OperationalPurgeJob.JobName).Subject;
        purge.Enabled.Should().BeTrue();
        purge.Schedule.Should().Be("Daily at 04:30 (UK time)");
        purge.Description.Should().StartWith("Dry run");
        purge.LastOutcome.Should().BeNull();
    }

    [Test]
    public async Task Jobs_Staff_AreForbidden()
    {
        var staff = await CreateStaffAsync(UserRole);

        await (await staff.Client.GetAsync("/api/jobs")).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await staff.Client.PostAsync($"/api/jobs/{OperationalPurgeJob.JobName}/run", null))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task RunJob_UnknownJob_Returns404()
    {
        var admin = await SignInAsAdminAsync();

        await (await admin.PostAsync("/api/jobs/no-such-job/run", null)).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task RunJob_OperationalPurge_RunsItAsADryRunByDefault()
    {
        var userId = await GetUserIdAsync(await SignInAsAdminAsync(), AdminUsername);
        await SeedOperationalDataAsync(userId);
        // The seed cleared the sessions, so sign in again (a new session, outside the counts).
        var admin = await SignInAsAdminAsync();

        var purge = await (await admin.PostAsync($"/api/jobs/{OperationalPurgeJob.JobName}/run", null))
            .ReadAsAsync<ScheduledJobResponse>();

        purge.LastOutcome.Should().Be(nameof(ScheduledJobOutcome.Succeeded));
        purge.LastCount.Should().Be(6, "one old row of each kind would go");
        purge.IsRunning.Should().BeFalse();
        (await CountOperationalDataAsync()).Should().Be(new OperationalCounts(2, 5, 2, 2, 2, 2), "a dry run deletes nothing");
    }

    [Test]
    public async Task OperationalPurge_NotADryRun_DeletesOnlyOldOperationalData()
    {
        await using var api = ApiWithTestJobs(("Retention:Operational:DryRun", "false"));
        var admin = await SignInAsAdminAsync();
        var userId = await GetUserIdAsync(admin, AdminUsername);
        await SeedOperationalDataAsync(userId);

        var row = await NewRunner(api).RunNowAsync(OperationalPurgeJob.JobName, CancellationToken.None);

        row.LastOutcome.Should().Be(ScheduledJobOutcome.Succeeded);
        row.LastCount.Should().Be(6);
        // Each kind keeps its recent row; the activity log keeps its old patient and account entries (and the sign-in
        // just made, and the account changes the setup wrote).
        var counts = await CountOperationalDataAsync();
        counts.Should().Be(new OperationalCounts(1, 4, 1, 1, 1, 1));
        var oldKept = await RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().ActivityEntries
            .Where(entry => entry.AtUtc < Now.AddMonths(-12))
            .Select(entry => entry.Action)
            .ToListAsync());
        oldKept.Should().BeEquivalentTo([ActivityAction.PatientViewed, ActivityAction.AccountCreated]);
    }

    [Test]
    public async Task OperationalPurge_MoreThanABatch_DeletesThemAll()
    {
        await using var api = ApiWithTestJobs(("Retention:Operational:DryRun", "false"));
        var userId = await GetUserIdAsync(await SignInAsAdminAsync(), AdminUsername);
        var old = Now.AddMonths(-7);
        await RunInScopeAsync(async services =>
        {
            var db = services.GetRequiredService<MedAppDbContext>();
            db.Notifications.AddRange(Enumerable.Range(0, OperationalDataRepository.BatchSize + 5).Select(_ => OldNotification(userId, old)));
            await db.SaveChangesAsync();
        });

        var row = await NewRunner(api).RunNowAsync(OperationalPurgeJob.JobName, CancellationToken.None);

        row.LastCount.Should().BeGreaterThanOrEqualTo(OperationalDataRepository.BatchSize + 5);
        (await RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().Notifications.CountAsync()))
            .Should().Be(0);
    }

    private sealed record OperationalCounts(
        int Sessions, int SignInEventsAndKept, int Notifications, int Notepads, int Challenges, int EmailCodes);

    /// <summary>
    /// For each kind of operational data, one row just past its period and one inside it; and in the activity log, old
    /// entries the purge must keep (a patient's view, an account change).
    /// </summary>
    private static async Task SeedOperationalDataAsync(Guid userId)
    {
        var now = Now;
        var other = await CreateStaffAsync(UserRole);
        await RunInScopeAsync(async services =>
        {
            var db = services.GetRequiredService<MedAppDbContext>();
            await db.UserSessions.ExecuteDeleteAsync();
            await db.ActivityEntries.ExecuteDeleteAsync();
            await db.Notifications.ExecuteDeleteAsync();

            var thirteenMonths = now.AddMonths(-13);
            db.UserSessions.AddRange(
                new UserSession(userId, thirteenMonths, thirteenMonths.AddHours(8), "192.0.2.1", "test"),
                new UserSession(userId, now.AddMonths(-11), now.AddMonths(-11).AddHours(8), "192.0.2.1", "test"));
            db.ActivityEntries.AddRange(
                Entry(ActivityAction.SignedIn, thirteenMonths),
                Entry(ActivityAction.SignedIn, now.AddMonths(-11)),
                Entry(ActivityAction.PatientViewed, thirteenMonths),
                Entry(ActivityAction.AccountCreated, thirteenMonths),
                Entry(ActivityAction.TwoFactorEnabled, now.AddDays(-1)));
            db.Notifications.AddRange(OldNotification(userId, now.AddMonths(-7)), OldNotification(userId, now.AddMonths(-5)));
            db.Notepads.AddRange(
                new Notepad { UserId = userId, Text = "Synthetic jotting.", LastUpdatedUtc = now.AddDays(-31), Version = 1 },
                new Notepad { UserId = other.UserId, Text = "Synthetic jotting.", LastUpdatedUtc = now.AddDays(-29), Version = 1 });
            db.SignInChallenges.AddRange(
                new SignInChallenge(userId, new string('a', 64), now.AddDays(-3)),
                new SignInChallenge(other.UserId, new string('b', 64), now));
            var expired = new EmailCode(userId, EmailCodePurpose.TurnOn, null, now.AddDays(-3));
            expired.Issue(new string('c', 64));
            var live = new EmailCode(other.UserId, EmailCodePurpose.TurnOn, null, now);
            live.Issue(new string('d', 64));
            db.EmailCodes.AddRange(expired, live);
            await db.SaveChangesAsync();
        });
        // Keep the two sessions above alone: the test's own sign-ins made more.
        await ExecuteSqlAsync($"DELETE FROM [UserSessions] WHERE [CreatedAtUtc] > {now.AddMonths(-1)}");
        await ExecuteSqlAsync($"DELETE FROM [ActivityEntries] WHERE [AtUtc] > {now.AddHours(-1)}");

        ActivityEntry Entry(ActivityAction action, DateTime at) => new()
        {
            Id = Guid.NewGuid(),
            AtUtc = at,
            Action = action,
            UserId = userId,
            Username = AdminUsername,
        };
    }

    private static Notification OldNotification(Guid userId, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Kind = NotificationKind.AppointmentBooked,
        Title = "Appointment booked",
        Message = "Synthetic message.",
        CreatedAtUtc = at,
    };

    private static Task<OperationalCounts> CountOperationalDataAsync() =>
        RunInScopeAsync(async services =>
        {
            var db = services.GetRequiredService<MedAppDbContext>();
            var cutoff = Now.AddHours(-1);
            return new OperationalCounts(
                await db.UserSessions.CountAsync(session => session.CreatedAtUtc < cutoff),
                await db.ActivityEntries.CountAsync(entry => entry.AtUtc < cutoff),
                await db.Notifications.CountAsync(),
                await db.Notepads.CountAsync(),
                await db.SignInChallenges.CountAsync(),
                await db.EmailCodes.CountAsync());
        });
}
