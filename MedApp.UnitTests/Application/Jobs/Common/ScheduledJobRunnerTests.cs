using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Jobs.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Domain.Jobs;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.JobsTestConstants;

namespace MedApp.UnitTests.Application.Jobs.Common;

[TestFixture]
public sealed class ScheduledJobRunnerTests
{
    /// <summary>The clock at <see cref="Now"/>, whose timers run a thousand times faster, so lease renewals come quickly.</summary>
    private sealed class FastTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, Scale(dueTime), Scale(period));

        private static TimeSpan Scale(TimeSpan span) => span == Timeout.InfiniteTimeSpan ? span : span / 1000;
    }

    private InMemoryScheduledJobRepository _repository = null!;
    private TestJob _job = null!;
    private JobsOptions _options = null!;
    private Mock<INotificationSender> _notifications = null!;
    private ServiceProvider _services = null!;
    private ScheduledJobScope? _scopeOfLastRun;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryScheduledJobRepository();
        _job = new TestJob();
        _options = new JobsOptions();
        _notifications = new Mock<INotificationSender>();
        _scopeOfLastRun = null;
        _job.During = _ => Task.CompletedTask;

        var identity = new Mock<IIdentityReadService>();
        identity.Setup(i => i.GetAdminIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([AdminId, OtherAdminId]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IScheduledJobRepository>(_repository);
        services.AddScoped<ScheduledJobScope>();
        services.AddScoped<IScheduledJob>(provider =>
        {
            // Records the scope a run was given, to check the runner marked it as a job's.
            _scopeOfLastRun = provider.GetRequiredService<ScheduledJobScope>();
            return _job;
        });
        services.AddSingleton(identity.Object);
        services.AddSingleton(_notifications.Object);
        services.AddSingleton(Options.Create(_options));
        services.AddSingleton<TimeProvider>(new FastTimeProvider(Now));
        services.AddSingleton<ScheduledJobRunner>();
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [TearDown]
    public void TearDown() => _services.Dispose();

    private ScheduledJobRunner Runner => _services.GetRequiredService<ScheduledJobRunner>();

    private ScheduledJob Row => _repository.Rows[JobName];

    [Test]
    public async Task RunDueAsync_NewIntervalJob_AddsItsRowAndRunsItAtOnce()
    {
        await Runner.RunDueAsync(CancellationToken.None);

        _job.RunsAt.Should().Equal(Now);
        Row.LastOutcome.Should().Be(ScheduledJobOutcome.Succeeded);
        Row.LastCount.Should().Be(7);
        Row.LastStartedAtUtc.Should().Be(Now);
        Row.LastFinishedAtUtc.Should().Be(Now);
        Row.NextRunUtc.Should().Be(Now.AddMinutes(15));
        Row.LeaseHolder.Should().BeNull();
    }

    [Test]
    public async Task RunDueAsync_NotDue_DoesNothing()
    {
        _repository.Rows[JobName] = CreateRow(Now.AddMinutes(1));

        await Runner.RunDueAsync(CancellationToken.None);

        _job.RunsAt.Should().BeEmpty();
        Row.LastOutcome.Should().BeNull();
    }

    [Test]
    public async Task RunDueAsync_LeaseHeldByAnother_DoesNothing()
    {
        var row = CreateRow(Now.AddMinutes(-5));
        row.LeaseHolder = "another-copy";
        row.LeaseUntilUtc = Now.AddMinutes(1);
        _repository.Rows[JobName] = row;

        await Runner.RunDueAsync(CancellationToken.None);

        _job.RunsAt.Should().BeEmpty();
        Row.LeaseHolder.Should().Be("another-copy");
    }

    [Test]
    public async Task RunDueAsync_LeaseOfAnotherEnded_TakesTheJob()
    {
        var row = CreateRow(Now.AddMinutes(-5));
        row.LeaseHolder = "a-copy-that-died";
        row.LeaseUntilUtc = Now.AddSeconds(-1);
        _repository.Rows[JobName] = row;

        await Runner.RunDueAsync(CancellationToken.None);

        _job.RunsAt.Should().Equal(Now);
        Row.LastOutcome.Should().Be(ScheduledJobOutcome.Succeeded);
    }

    [Test]
    public async Task RunDueAsync_Runs_InAScopeMarkedAsAJobs()
    {
        await Runner.RunDueAsync(CancellationToken.None);

        _scopeOfLastRun!.IsJob.Should().BeTrue();
    }

    [Test]
    public async Task RunDueAsync_SwitchedOff_RecordsSkippedAndMovesOn()
    {
        _options.JobEnabled[JobName] = false;

        await Runner.RunDueAsync(CancellationToken.None);

        _job.RunsAt.Should().BeEmpty();
        Row.LastOutcome.Should().Be(ScheduledJobOutcome.Skipped);
        Row.LastCount.Should().BeNull();
        Row.NextRunUtc.Should().Be(Now.AddMinutes(15));
        Row.LeaseHolder.Should().BeNull();
    }

    [Test]
    public async Task RunDueAsync_JobFails_RecordsTheErrorTypeAndRetriesSooner()
    {
        _job.Throws = new InvalidOperationException("A message that mustn't be stored.");
        var daily = new TestJob(JobSchedule.DailyAt(new TimeOnly(4, 30))) { Throws = _job.Throws };
        _job = daily;
        _repository.Rows[JobName] = CreateRow(Now);

        await Runner.RunDueAsync(CancellationToken.None);

        Row.LastOutcome.Should().Be(ScheduledJobOutcome.Failed);
        Row.LastErrorType.Should().Be(nameof(InvalidOperationException));
        Row.ConsecutiveFailures.Should().Be(1);
        Row.LastCount.Should().BeNull();
        Row.NextRunUtc.Should().Be(Now.Add(JobSchedule.LongestRetryDelay));
    }

    [Test]
    public async Task RunNowAsync_ThirdFailureInARow_TellsEveryAdminOnce()
    {
        _job.Throws = new InvalidOperationException("Synthetic failure.");
        var sent = new List<Notification>();
        _notifications.Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((notification, _) => sent.Add(notification))
            .Returns(Task.CompletedTask);

        for (var i = 0; i < 2; i++)
            await Runner.RunNowAsync(JobName, CancellationToken.None);
        sent.Should().BeEmpty();

        await Runner.RunNowAsync(JobName, CancellationToken.None);
        sent.Select(n => n.UserId).Should().BeEquivalentTo([AdminId, OtherAdminId]);
        sent.Should().OnlyContain(n => n.Kind == NotificationKind.ScheduledJobFailing && !n.Message.Contains("Synthetic failure"));

        await Runner.RunNowAsync(JobName, CancellationToken.None);
        sent.Should().HaveCount(2, "only the third failure in a row tells them");
        Row.ConsecutiveFailures.Should().Be(4);
    }

    [Test]
    public async Task RunNowAsync_SuccessAfterFailures_ResetsThemAndTheErrorType()
    {
        _job.Throws = new InvalidOperationException();
        await Runner.RunNowAsync(JobName, CancellationToken.None);
        _job.Throws = null;

        var row = await Runner.RunNowAsync(JobName, CancellationToken.None);

        row.ConsecutiveFailures.Should().Be(0);
        row.LastErrorType.Should().BeNull();
        row.LastOutcome.Should().Be(ScheduledJobOutcome.Succeeded);
    }

    [Test]
    public async Task RunNowAsync_NotDue_RunsItAnyway()
    {
        _repository.Rows[JobName] = CreateRow(Now.AddHours(1));

        var row = await Runner.RunNowAsync(JobName, CancellationToken.None);

        _job.RunsAt.Should().Equal(Now);
        row.LastCount.Should().Be(7);
    }

    [Test]
    public async Task RunNowAsync_UnknownJob_ThrowsNotFound()
    {
        var act = () => Runner.RunNowAsync("no-such-job", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task RunNowAsync_SwitchedOff_ThrowsConflict()
    {
        _options.JobEnabled[JobName] = false;

        var act = () => Runner.RunNowAsync(JobName, CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).WithMessage("*switched off*");
        _job.RunsAt.Should().BeEmpty();
    }

    [Test]
    public async Task RunNowAsync_RunningAlready_ThrowsConflict()
    {
        var row = CreateRow();
        row.LeaseHolder = "another-copy";
        row.LeaseUntilUtc = Now.AddMinutes(5);
        _repository.Rows[JobName] = row;

        var act = () => Runner.RunNowAsync(JobName, CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).WithMessage("*running already*");
    }

    [Test]
    public async Task RunNowAsync_LongRun_RenewsTheLease()
    {
        // 3 minutes is 180 ms on this clock.
        _job.During = ct => Task.Delay(TimeSpan.FromMilliseconds(700), ct);

        await Runner.RunNowAsync(JobName, CancellationToken.None);

        _repository.Renewals.Should().BeGreaterThanOrEqualTo(2);
        Row.LastOutcome.Should().Be(ScheduledJobOutcome.Succeeded);
    }

    [Test]
    public async Task RunNowAsync_LeaseLostWhileRunning_StopsTheJobAndRecordsNothing()
    {
        _repository.RenewalsSucceed = false;
        _job.During = ct => Task.Delay(TimeSpan.FromSeconds(30), ct);

        var row = await Runner.RunNowAsync(JobName, CancellationToken.None);

        row.LastOutcome.Should().BeNull("the run was stopped, not finished");
        row.LeaseHolder.Should().Be(Runner.Holder, "the lease is left to end, as the other runner's view decides");
    }

    [Test]
    public async Task RunNowAsync_Cancelled_GivesTheLeaseUpUnrecorded()
    {
        using var cts = new CancellationTokenSource();
        _job.During = async ct =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
        };

        var act = () => Runner.RunNowAsync(JobName, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Row.LeaseHolder.Should().BeNull();
        Row.LeaseUntilUtc.Should().BeNull();
        Row.LastOutcome.Should().BeNull();
    }

    [Test]
    public void Holder_IsUniqueForEachRunner()
    {
        var first = ActivatorUtilities.CreateInstance<ScheduledJobRunner>(_services);
        var second = ActivatorUtilities.CreateInstance<ScheduledJobRunner>(_services);

        first.Holder.Should().NotBe(second.Holder).And.StartWith(Environment.MachineName);
    }

    [Test]
    public void ErrorType_IsTheTypeNameOnly()
    {
        ScheduledJobRunner.ErrorType(new TimeoutException("Value 9990000001 timed out.")).Should().Be(nameof(TimeoutException));
    }

    [Test]
    public void SystemActivityContext_IsTheSystemWithNoUserOrAddress()
    {
        SystemActivityContext.Instance.Actor.Should().Be(new ActivityActor(null, "medapp-jobs", null));
    }
}
