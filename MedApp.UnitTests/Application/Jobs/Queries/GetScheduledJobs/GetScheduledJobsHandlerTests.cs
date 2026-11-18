using FluentAssertions;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Jobs.Queries.GetScheduledJobs;
using MedApp.Application.Jobs.Repositories;
using MedApp.Domain.Jobs;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.JobsTestConstants;

namespace MedApp.UnitTests.Application.Jobs.Queries.GetScheduledJobs;

[TestFixture]
public sealed class GetScheduledJobsHandlerTests
{
    [Test]
    public async Task Handle_ListsEveryJobByTitle_WithItsRowAndSwitch()
    {
        var ran = CreateRow();
        ran.LastOutcome = ScheduledJobOutcome.Succeeded;
        ran.LastCount = 3;
        var repository = new Mock<IScheduledJobRepository>();
        using var cts = new CancellationTokenSource();
        repository.Setup(r => r.GetAllAsync(cts.Token)).ReturnsAsync([ran]);
        var options = new JobsOptions();
        options.JobEnabled["zz-other"] = false;
        IScheduledJob[] jobs = [new TestJob { Name = "zz-other" }, new TestJob()];

        var handler = new GetScheduledJobsHandler(jobs, repository.Object, Options.Create(options), new FixedTimeProvider(Now));
        var response = await handler.Handle(new GetScheduledJobsQuery(), cts.Token);

        // Same titles, so by name.
        response.Select(job => job.Name).Should().Equal(JobName, "zz-other");
        response[0].LastCount.Should().Be(3);
        response[0].Enabled.Should().BeTrue();
        response[1].LastOutcome.Should().BeNull("it hasn't been seen by a runner yet");
        response[1].Enabled.Should().BeFalse();
    }
}
