using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.API.Jobs;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Jobs.Repositories;
using MedApp.Domain.Jobs;
using MedApp.Infrastructure.Common.Extensions;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.JobsTestConstants;

namespace MedApp.UnitTests.Api.Jobs;

[TestFixture]
public sealed class ScheduledJobsHealthCheckTests
{
    private Mock<IScheduledJobRepository> _repository = null!;
    private JobsOptions _options = null!;
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IScheduledJobRepository>();
        _options = new JobsOptions();
        var services = new ServiceCollection();
        services.AddSingleton(_repository.Object);
        services.AddScoped<IScheduledJob>(_ => new TestJob());
        _services = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown() => _services.Dispose();

    private Task<HealthCheckResult> CheckAsync() =>
        new ScheduledJobsHealthCheck(
                _services.GetRequiredService<IServiceScopeFactory>(), Options.Create(_options), new FixedTimeProvider(Now))
            .CheckHealthAsync(new HealthCheckContext());

    private void Given(params ScheduledJob[] rows) =>
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);

    [Test]
    public async Task Check_JobsOnTime_IsHealthy()
    {
        Given(CreateRow(Now.AddMinutes(-30)));

        (await CheckAsync()).Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public async Task Check_JobTwoIntervalsOverdue_IsDegradedAndNamesIt()
    {
        Given(CreateRow(Now.AddMinutes(-31)));

        var result = await CheckAsync();

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain(JobName);
    }

    [Test]
    public async Task Check_OverdueButSwitchedOff_IsHealthy()
    {
        Given(CreateRow(Now.AddDays(-1)));
        _options.JobEnabled[JobName] = false;

        (await CheckAsync()).Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public async Task Check_JobsCantBeRead_IsDegradedNeverUnhealthy()
    {
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("9990000001"));

        var result = await CheckAsync();

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain(nameof(TimeoutException)).And.NotContain("9990000001");
    }
}

[TestFixture]
public sealed class ScheduledJobHostTests
{
    [Test]
    public async Task RunDueAsync_RunnerFails_LogsAndCarriesOn()
    {
        var runner = new Mock<IScheduledJobRunner>();
        runner.Setup(r => r.RunDueAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        var host = new ScheduledJobHost(
            runner.Object, Options.Create(new JobsOptions()), TimeProvider.System, NullLogger<ScheduledJobHost>.Instance);

        var act = () => host.RunDueAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        runner.Verify(r => r.RunDueAsync(CancellationToken.None));
    }

    [Test]
    public async Task Start_JobsSwitchedOff_NeverRunsThem()
    {
        var runner = new Mock<IScheduledJobRunner>();
        var host = new ScheduledJobHost(
            runner.Object, Options.Create(new JobsOptions { Enabled = false }), TimeProvider.System,
            NullLogger<ScheduledJobHost>.Instance);

        await host.StartAsync(CancellationToken.None);
        await host.ExecuteTask!;
        await host.StopAsync(CancellationToken.None);

        runner.Verify(r => r.RunDueAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Start_JobsOn_RunsTheDueOnesAtOnce()
    {
        var ran = new TaskCompletionSource();
        var runner = new Mock<IScheduledJobRunner>();
        runner.Setup(r => r.RunDueAsync(It.IsAny<CancellationToken>())).Callback(() => ran.TrySetResult()).Returns(Task.CompletedTask);
        var host = new ScheduledJobHost(
            runner.Object, Options.Create(new JobsOptions()), TimeProvider.System, NullLogger<ScheduledJobHost>.Instance);

        await host.StartAsync(CancellationToken.None);
        await ran.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await host.StopAsync(CancellationToken.None);

        runner.Verify(r => r.RunDueAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}

[TestFixture]
public sealed class RunJobCommandTests
{
    private static IServiceProvider Services(IScheduledJobRunner runner) =>
        new ServiceCollection().AddLogging().AddSingleton(runner).BuildServiceProvider();

    [TestCase(ScheduledJobOutcome.Succeeded, 0)]
    [TestCase(ScheduledJobOutcome.Failed, 1)]
    public async Task RunAsync_ExitCodeFollowsTheOutcome(ScheduledJobOutcome outcome, int exitCode)
    {
        var row = CreateRow();
        row.LastOutcome = outcome;
        var runner = new Mock<IScheduledJobRunner>();
        runner.Setup(r => r.RunNowAsync(JobName, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        (await RunJobCommand.RunAsync(Services(runner.Object), JobName, CancellationToken.None)).Should().Be(exitCode);
    }

    [Test]
    public async Task RunAsync_RefusedOrUnknown_ExitsWithOne()
    {
        var runner = new Mock<IScheduledJobRunner>();
        runner.Setup(r => r.RunNowAsync("busy", It.IsAny<CancellationToken>())).ThrowsAsync(new ConflictException("Running already."));
        runner.Setup(r => r.RunNowAsync("none", It.IsAny<CancellationToken>())).ThrowsAsync(new NotFoundException("No such job."));

        (await RunJobCommand.RunAsync(Services(runner.Object), "busy", CancellationToken.None)).Should().Be(1);
        (await RunJobCommand.RunAsync(Services(runner.Object), "none", CancellationToken.None)).Should().Be(1);
    }
}

[TestFixture]
public sealed class JobSettingsTests
{
    [Test]
    public void BindJobs_ReadsTheSwitchAndEachJobs()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jobs:Enabled"] = "false",
            ["Jobs:operational-purge:Enabled"] = "false",
            ["Jobs:other-job:Enabled"] = "true",
            ["Jobs:not-a-bool:Enabled"] = "maybe",
        }).Build();
        var options = new JobsOptions();

        ServiceExtensions.BindJobs(configuration.GetSection(JobsOptions.Section), options);

        options.Enabled.Should().BeFalse();
        options.IsJobEnabled("operational-purge").Should().BeFalse();
        options.IsJobEnabled("Operational-Purge").Should().BeFalse("names are matched ignoring case, as configuration keys are");
        options.IsJobEnabled("other-job").Should().BeTrue();
        options.IsJobEnabled("not-a-bool").Should().BeTrue();
        options.IsJobEnabled("unnamed").Should().BeTrue();
    }

    [Test]
    public void BindJobs_NothingSet_EverythingOn()
    {
        var options = new JobsOptions();

        ServiceExtensions.BindJobs(new ConfigurationBuilder().Build().GetSection(JobsOptions.Section), options);

        options.Enabled.Should().BeTrue();
        options.IsJobEnabled("operational-purge").Should().BeTrue();
    }

    [Test]
    public void ActivityContext_InAJobsScope_IsTheSystem_OtherwiseTheRequest()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        services.AddScoped<ScheduledJobScope>();
        services.AddScoped<HttpActivityContext>();
        services.AddScoped(MedApp.API.Common.Extensions.ServiceExtensions.ActivityContextFor);
        using var provider = services.BuildServiceProvider();

        using (var request = provider.CreateScope())
            request.ServiceProvider.GetRequiredService<IActivityContext>().Should().BeOfType<HttpActivityContext>();

        using var job = provider.CreateScope();
        job.ServiceProvider.GetRequiredService<ScheduledJobScope>().IsJob = true;
        job.ServiceProvider.GetRequiredService<IActivityContext>().Should().BeSameAs(SystemActivityContext.Instance);
    }
}
