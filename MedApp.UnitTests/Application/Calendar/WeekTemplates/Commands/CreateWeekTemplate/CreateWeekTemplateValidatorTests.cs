using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;

/// <summary>
/// The rules every week template command shares (<see cref="WeekTemplateValidation{T}"/>), through the create command,
/// and its own: an owner.
/// </summary>
[TestFixture]
public sealed class CreateWeekTemplateValidatorTests
{
    private readonly CreateWeekTemplateValidator _validator = new();

    private static CreateWeekTemplateCommand Command(string name) =>
        new(StaffUserId, name, DayTemplateId, null, null, null, OtherDayTemplateId, StaffActor);

    [Test]
    public void Validate_NameAndDays_Passes()
    {
        _validator.Validate(Command(WeekTemplateName)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoDays_Passes()
    {
        _validator.Validate(new CreateWeekTemplateCommand(StaffUserId, WeekTemplateName, null, null, null, null, null, StaffActor)).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Validate_BlankName_Fails(string name)
    {
        _validator.Validate(Command(name)).Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage == "Name is required.");
    }

    [Test]
    public void Validate_NameAtTheLimit_Passes()
    {
        _validator.Validate(Command(new string('w', CalendarLimits.MaxTemplateNameLength))).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NameTooLong_Fails()
    {
        _validator.Validate(Command(new string('w', CalendarLimits.MaxTemplateNameLength + 1)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("must not exceed"));
    }

    [Test]
    public void Validate_NoOwner_FailsSayingMemberOfStaffIsRequired()
    {
        var result = _validator.Validate(Command(WeekTemplateName) with { OwnerUserId = Guid.Empty });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "OwnerUserId")
            .Which.ErrorMessage.Should().Be("Member of staff is required.");
    }
}
