using FluentAssertions;
using MedApp.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;

/// <summary>The update command's own rule; the shared ones are in <c>CreateWeekTemplateValidatorTests</c>.</summary>
[TestFixture]
public sealed class UpdateWeekTemplateValidatorTests
{
    private readonly UpdateWeekTemplateValidator _validator = new();

    [Test]
    public void Validate_TemplateAndName_Passes()
    {
        _validator.Validate(new UpdateWeekTemplateCommand(WeekTemplateId, WeekTemplateName, DayTemplateId, null, null, null, null, StaffActor))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoTemplate_Fails()
    {
        _validator.Validate(new UpdateWeekTemplateCommand(Guid.Empty, WeekTemplateName, null, null, null, null, null, StaffActor))
            .Errors.Should().ContainSingle(e => e.PropertyName == "WeekTemplateId" && e.ErrorMessage == "Week template id is required.");
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(new UpdateWeekTemplateCommand(WeekTemplateId, " ", null, null, null, null, null, StaffActor))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Name");
    }
}
