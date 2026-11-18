using FluentAssertions;
using MedApp.Application.Monitoring.Commands.UpdateMonitoringRule;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.Commands.UpdateMonitoringRule;

/// <summary>The edit checks are the add checks (MonitoringRuleInputValidator, tested in full there).</summary>
[TestFixture]
public sealed class UpdateMonitoringRuleValidatorTests
{
    private readonly UpdateMonitoringRuleValidator _validator = new();

    [Test]
    public void Validate_SwitchedOffRuleForAStage_Passes()
    {
        var command = new UpdateMonitoringRuleCommand(RuleId, OwnerId, Staff.UserId, "StageUnlocked", "Urgent", null, ConsultationDefinitionId, false);

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_StageOnAnEventWithoutOne_Fails()
    {
        var command = new UpdateMonitoringRuleCommand(RuleId, OwnerId, null, "PathwayBlocked", null, null, ConsultationDefinitionId, true);

        _validator.Validate(command).Errors.Should().ContainSingle().Which.PropertyName.Should().Be("StageDefinitionId");
    }
}
