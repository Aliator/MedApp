using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Jobs.Commands.RunScheduledJob;
using MedApp.Application.Jobs.Queries.GetScheduledJobs;
using MedApp.Contracts.Jobs.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class JobsControllerTests
{
    private static readonly ScheduledJobResponse Job = new() { Name = "operational-purge", Title = "Operational data purge" };

    [Test]
    public async Task GetJobs_ReturnsEveryJob()
    {
        IReadOnlyList<ScheduledJobResponse> jobs = [Job];
        var mediator = new RecordingMediator(_ => jobs);
        using var cts = new CancellationTokenSource();

        var result = await new JobsController(mediator).WithRequest(SignedIn("Admin")).GetJobs(cts.Token);

        mediator.Request.Should().Be(new GetScheduledJobsQuery());
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(jobs);
    }

    [Test]
    public async Task RunJob_RunsTheNamedJob()
    {
        var mediator = new RecordingMediator(_ => Job);

        var result = await new JobsController(mediator).WithRequest(SignedIn("Admin"))
            .RunJob("operational-purge", CancellationToken.None);

        mediator.Request.Should().Be(new RunScheduledJobCommand("operational-purge"));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(Job);
    }

    [TestCase(nameof(JobsController.GetJobs))]
    [TestCase(nameof(JobsController.RunJob))]
    public async Task Actions_AreForAdminsOnly(string action)
    {
        (await ControllerAuthorisation.MayCallAsync<JobsController>(action, SignedIn("Admin"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<JobsController>(action, SignedIn("User"))).Should().BeFalse();
    }
}
