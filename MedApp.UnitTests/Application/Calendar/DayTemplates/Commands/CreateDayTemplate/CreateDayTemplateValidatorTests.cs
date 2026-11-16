using FluentAssertions;
using MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;

/// <summary>The create command has only the shared rules (see <c>DayTemplateValidationTests</c>).</summary>
[TestFixture]
public sealed class CreateDayTemplateValidatorTests
{
    private readonly CreateDayTemplateValidator _validator = new();

    [Test]
    public void Validate_NameAndBlocks_Passes()
    {
        _validator.Validate(new CreateDayTemplateCommand(DayTemplateName, [BlockValue(12, 13)])).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_BlankNameAndBadBlock_FailsForBoth()
    {
        var result = _validator.Validate(new CreateDayTemplateCommand(" ", [BlockValue(19, 20)]));

        result.Errors.Should().HaveCount(2);
    }
}
