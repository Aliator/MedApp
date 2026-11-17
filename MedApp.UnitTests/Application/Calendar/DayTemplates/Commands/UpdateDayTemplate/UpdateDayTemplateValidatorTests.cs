using FluentAssertions;
using MedApp.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;

/// <summary>The update command's own rule; the shared ones are in <c>DayTemplateValidationTests</c>.</summary>
[TestFixture]
public sealed class UpdateDayTemplateValidatorTests
{
    private readonly UpdateDayTemplateValidator _validator = new();

    [Test]
    public void Validate_TemplateNameAndBlocks_Passes()
    {
        _validator.Validate(new UpdateDayTemplateCommand(DayTemplateId, DayTemplateName, [BlockValue(12, 13)], StaffActor)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoTemplate_Fails()
    {
        var result = _validator.Validate(new UpdateDayTemplateCommand(Guid.Empty, DayTemplateName, [], StaffActor));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "DayTemplateId" && e.ErrorMessage == "Day template id is required.");
    }

    [Test]
    public void Validate_OverlappingBlocks_Fails()
    {
        _validator.Validate(new UpdateDayTemplateCommand(DayTemplateId, DayTemplateName, [BlockValue(12, 14), BlockValue(13, 15)], StaffActor))
            .IsValid.Should().BeFalse();
    }
}
