using FluentAssertions;
using FluentValidation.TestHelper;
using MedApp.Application.Jobs.Commands.RunScheduledJob;
using MedApp.Application.Jobs.Common;
using MedApp.Domain.Jobs;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.JobsTestConstants;

namespace MedApp.UnitTests.Application.Jobs.Commands.RunScheduledJob;

[TestFixture]
public sealed class RunScheduledJobHandlerTests
{
    [Test]
    public async Task Handle_RunsItThroughTheRunner_AndReturnsIt()
    {
        var row = CreateRow(Now.AddMinutes(15));
        row.LastOutcome = ScheduledJobOutcome.Succeeded;
        row.LastCount = 7;
        var runner = new Mock<IScheduledJobRunner>();
        using var cts = new CancellationTokenSource();
        runner.Setup(r => r.RunNowAsync(JobName, cts.Token)).ReturnsAsync(row);

        var handler = new RunScheduledJobHandler(
            runner.Object, [new TestJob()], Options.Create(new JobsOptions()), new FixedTimeProvider(Now));
        var response = await handler.Handle(new RunScheduledJobCommand(JobName), cts.Token);

        response.Name.Should().Be(JobName);
        response.LastOutcome.Should().Be("Succeeded");
        response.LastCount.Should().Be(7);
        response.NextRunUtc.Should().Be(Now.AddMinutes(15));
    }
}

[TestFixture]
public sealed class RunScheduledJobValidatorTests
{
    private readonly RunScheduledJobValidator _validator = new();

    [Test]
    public void Validate_AName_IsValid() =>
        _validator.TestValidate(new RunScheduledJobCommand(JobName)).ShouldNotHaveAnyValidationErrors();

    [TestCase("")]
    [TestCase(" ")]
    public void Validate_NoName_IsInvalid(string name) =>
        _validator.TestValidate(new RunScheduledJobCommand(name)).ShouldHaveValidationErrorFor(x => x.Name);

    [Test]
    public void Validate_NameTooLong_IsInvalid() =>
        _validator.TestValidate(new RunScheduledJobCommand(new string('a', ScheduledJob.MaxNameLength + 1)))
            .ShouldHaveValidationErrorFor(x => x.Name);
}
