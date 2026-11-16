using FluentAssertions;
using MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ActivityTypes.Commands.CreateActivityType;

[TestFixture]
public sealed class CreateActivityTypeValidatorTests
{
    private readonly CreateActivityTypeValidator _validator = new();

    [Test]
    public void Validate_NameAndPaletteColour_Passes()
    {
        _validator.Validate(new CreateActivityTypeCommand(ActivityTypeName, TidyColour)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ColourInOtherCaseWithSpaces_Passes()
    {
        _validator.Validate(new CreateActivityTypeCommand(ActivityTypeName, UntidyColour)).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_BlankName_Fails(string name)
    {
        var result = _validator.Validate(new CreateActivityTypeCommand(name, TidyColour));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage == "Name is required.");
    }

    [Test]
    public void Validate_NameAtTheLimit_Passes()
    {
        var name = new string('a', CalendarLimits.MaxActivityTypeNameLength);

        _validator.Validate(new CreateActivityTypeCommand(name, TidyColour)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NameTooLong_Fails()
    {
        var name = new string('a', CalendarLimits.MaxActivityTypeNameLength + 1);

        var result = _validator.Validate(new CreateActivityTypeCommand(name, TidyColour));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("must not exceed"));
    }

    [TestCase(NotAColour)]
    [TestCase("")]
    [TestCase(null)]
    public void Validate_NotAPaletteColour_FailsListingThePalette(string? colour)
    {
        var result = _validator.Validate(new CreateActivityTypeCommand(ActivityTypeName, colour!));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Colour" && e.ErrorMessage.Contains(CalendarColours.Names));
    }
}
