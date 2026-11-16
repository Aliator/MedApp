using FluentAssertions;
using MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;

[TestFixture]
public sealed class SetStaffWeekTemplateValidatorTests
{
    private readonly SetStaffWeekTemplateValidator _validator = new();

    private static SetStaffWeekTemplateCommand Valid() => new(StaffUserId, WeekTemplateId, StaffActor);

    [Test]
    public void Validate_WithAWeekTemplate_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_None_Passes()
    {
        _validator.Validate(Valid() with { WeekTemplateId = null }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyWeekTemplateId_Fails()
    {
        _validator.Validate(Valid() with { WeekTemplateId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Week template id must not be empty.");
    }

    [Test]
    public void Validate_NoMemberOfStaff_Fails()
    {
        _validator.Validate(Valid() with { StaffUserId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Member of staff is required.");
    }
}
