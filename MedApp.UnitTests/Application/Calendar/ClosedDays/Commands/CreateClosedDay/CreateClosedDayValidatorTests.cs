using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

[TestFixture]
public sealed class CreateClosedDayValidatorTests
{
    private readonly CreateClosedDayValidator _validator = new();

    private IEnumerable<string> Errors(CreateClosedDayCommand command) =>
        _validator.Validate(command).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_DateAndName_Passes()
    {
        _validator.Validate(new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NameAtTheLimit_Passes()
    {
        _validator.Validate(new CreateClosedDayCommand(ClosedWednesday, new string('a', CalendarLimits.MaxClosedDayNameLength), AdminActor))
            .IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_NoName_Fails(string name)
    {
        Errors(new CreateClosedDayCommand(ClosedWednesday, name, AdminActor)).Should().Equal("Name is required.");
    }

    [Test]
    public void Validate_NameTooLong_Fails()
    {
        Errors(new CreateClosedDayCommand(ClosedWednesday, new string('a', CalendarLimits.MaxClosedDayNameLength + 1), AdminActor))
            .Should().Equal($"Name must not exceed {CalendarLimits.MaxClosedDayNameLength} characters.");
    }

    [Test]
    public void Validate_NoDate_Fails()
    {
        Errors(new CreateClosedDayCommand(default, ClosedDayName, AdminActor)).Should().Equal("Date is required.");
    }
}
