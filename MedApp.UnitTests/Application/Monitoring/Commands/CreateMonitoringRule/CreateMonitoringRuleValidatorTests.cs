using FluentAssertions;
using MedApp.Application.Monitoring.Commands.CreateMonitoringRule;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.Commands.CreateMonitoringRule;

[TestFixture]
public sealed class CreateMonitoringRuleValidatorTests
{
    private readonly CreateMonitoringRuleValidator _validator = new();

    private static CreateMonitoringRuleCommand Valid() =>
        new(OwnerId, Staff.UserId, "StageFinished", "High", TemplateId, ConsultationDefinitionId, true);

    [Test]
    public void Validate_EveryCondition_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoConditions_Passes()
    {
        _validator.Validate(new CreateMonitoringRuleCommand(OwnerId, null, "PathwayCreated", null, null, null, false))
            .IsValid.Should().BeTrue();
    }

    [TestCase("stagefinished")]
    [TestCase("DueDateChanged")]
    public void Validate_EventInAnyCase_Passes(string monitoringEvent)
    {
        _validator.Validate(Valid() with { Event = monitoringEvent, StageDefinitionId = null }).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("StageStarted")]
    [TestCase("1")]
    public void Validate_UnknownEvent_FailsListingTheEvents(string monitoringEvent)
    {
        var result = _validator.Validate(Valid() with { Event = monitoringEvent });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Event")
            .Which.ErrorMessage.Should().StartWith("Event must be one of: PathwayCreated, StageFinished");
    }

    [TestCase("Critical")]
    [TestCase("")]
    [TestCase("2")]
    public void Validate_UnknownPriority_Fails(string priority)
    {
        _validator.Validate(Valid() with { MinimumPriority = priority }).Errors
            .Should().ContainSingle().Which.PropertyName.Should().Be("MinimumPriority");
    }

    [Test]
    public void Validate_EmptyIds_Fail()
    {
        var result = _validator.Validate(Valid() with { SubjectUserId = Guid.Empty, TemplateId = Guid.Empty, StageDefinitionId = Guid.Empty });

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["SubjectUserId", "TemplateId", "StageDefinitionId"]);
    }

    [TestCase("StageFinished", true)]
    [TestCase("StageUnlocked", true)]
    [TestCase("StageFinishedWithoutVisit", true)]
    [TestCase("PathwayCreated", false)]
    [TestCase("PathwayAssigned", false)]
    public void Validate_StageChosen_PassesOnlyForStageEvents(string monitoringEvent, bool isValid)
    {
        var result = _validator.Validate(Valid() with { Event = monitoringEvent });

        result.IsValid.Should().Be(isValid);
        if (!isValid)
            result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("A stage can only be chosen for a stage being finished, finished without its visit, or unlocked.");
    }
}
