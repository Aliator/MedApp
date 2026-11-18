using FluentAssertions;
using MedApp.Application.Jobs.Common;
using MedApp.Domain.Jobs;
using static MedApp.UnitTests.Common.Constants.JobsTestConstants;

namespace MedApp.UnitTests.Application.Jobs.Common;

[TestFixture]
public sealed class ScheduledJobMapperTests
{
    private static readonly TestJob Job = new();

    [Test]
    public void ToResponse_NoRowYet_GivesTheJobWithNothingRun()
    {
        var response = ScheduledJobMapper.ToResponse(Job, null, enabled: true, Now);

        response.Name.Should().Be(JobName);
        response.Title.Should().Be(JobTitle);
        response.Description.Should().Be(JobDescription);
        response.Schedule.Should().Be("Every 15 minutes");
        response.Enabled.Should().BeTrue();
        response.IsRunning.Should().BeFalse();
        response.IsOverdue.Should().BeFalse();
        response.NextRunUtc.Should().BeNull();
        response.LastOutcome.Should().BeNull();
        response.ConsecutiveFailures.Should().Be(0);
    }

    [Test]
    public void ToResponse_Row_CopiesItsLastRun()
    {
        var row = CreateRow(Now.AddMinutes(10));
        row.LastStartedAtUtc = Now.AddMinutes(-5);
        row.LastFinishedAtUtc = Now.AddMinutes(-4);
        row.LastOutcome = ScheduledJobOutcome.Failed;
        row.ConsecutiveFailures = 2;
        row.LastErrorType = "SqlException";

        var response = ScheduledJobMapper.ToResponse(Job, row, enabled: false, Now);

        response.Enabled.Should().BeFalse();
        response.NextRunUtc.Should().Be(Now.AddMinutes(10));
        response.LastStartedAtUtc.Should().Be(Now.AddMinutes(-5));
        response.LastFinishedAtUtc.Should().Be(Now.AddMinutes(-4));
        response.LastOutcome.Should().Be("Failed");
        response.ConsecutiveFailures.Should().Be(2);
        response.LastErrorType.Should().Be("SqlException");
    }

    [Test]
    public void ToResponse_LeaseHeld_IsRunning()
    {
        var row = CreateRow();
        row.LeaseHolder = "a-copy";
        row.LeaseUntilUtc = Now.AddMinutes(5);

        ScheduledJobMapper.ToResponse(Job, row, enabled: true, Now).IsRunning.Should().BeTrue();
        row.LeaseUntilUtc = Now.AddMinutes(-1);
        ScheduledJobMapper.ToResponse(Job, row, enabled: true, Now).IsRunning.Should().BeFalse("the lease has ended");
    }

    [Test]
    public void IsOverdue_MoreThanTwoIntervalsLate_OnlyWhenOnAndNotRunning()
    {
        var late = CreateRow(Now.AddMinutes(-31));

        ScheduledJobMapper.IsOverdue(Job.Schedule, late, enabled: true, Now).Should().BeTrue();
        ScheduledJobMapper.IsOverdue(Job.Schedule, late, enabled: false, Now).Should().BeFalse();
        ScheduledJobMapper.IsOverdue(Job.Schedule, CreateRow(Now.AddMinutes(-30)), enabled: true, Now).Should().BeFalse();
        ScheduledJobMapper.IsOverdue(Job.Schedule, null, enabled: true, Now).Should().BeFalse();

        late.LeaseUntilUtc = Now.AddMinutes(1);
        ScheduledJobMapper.IsOverdue(Job.Schedule, late, enabled: true, Now).Should().BeFalse("it's running");
    }
}
