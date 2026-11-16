using FluentAssertions;
using MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;

[TestFixture]
public sealed class UpdateActivityTypeValidatorTests
{
    private readonly UpdateActivityTypeValidator _validator = new();

    [Test]
    public void Validate_NothingToChange_Passes()
    {
        _validator.Validate(new UpdateActivityTypeCommand(Guid.NewGuid(), null, null)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NameAndColour_Passes()
    {
        _validator.Validate(new UpdateActivityTypeCommand(Guid.NewGuid(), ActivityTypeName, UntidyColour)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoActivityType_Fails()
    {
        var result = _validator.Validate(new UpdateActivityTypeCommand(Guid.Empty, null, null));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "ActivityTypeId");
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Validate_BlankName_Fails(string name)
    {
        var result = _validator.Validate(new UpdateActivityTypeCommand(Guid.NewGuid(), name, null));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage == "Name must not be blank.");
    }

    [Test]
    public void Validate_NameTooLong_Fails()
    {
        var name = new string('a', CalendarLimits.MaxActivityTypeNameLength + 1);

        var result = _validator.Validate(new UpdateActivityTypeCommand(Guid.NewGuid(), name, null));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("must not exceed"));
    }

    [TestCase(NotAColour)]
    [TestCase("")]
    public void Validate_NotAPaletteColour_Fails(string colour)
    {
        var result = _validator.Validate(new UpdateActivityTypeCommand(Guid.NewGuid(), null, colour));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Colour" && e.ErrorMessage.Contains(CalendarColours.Names));
    }
}
