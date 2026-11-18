using FluentAssertions;
using MedApp.UnitTests.Common.Fixtures;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;

[TestFixture]
public sealed class ApplyDayTemplateValidatorTests
{
    private readonly ApplyDayTemplateValidator _validator = new(InMemoryClosedDays.WorkingDays(CreateClosedDay()));

    private static ApplyDayTemplateCommand Valid() => new(StaffUserId, NextMonday, DayTemplateId, StaffActor);

    [Test]
    public void Validate_WeekdayWithADayTemplate_Passes()
    {
        _validator.ValidateNow(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoDayTemplate_Passes()
    {
        _validator.ValidateNow(Valid() with { DayTemplateId = null }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyDayTemplateId_Fails()
    {
        _validator.ValidateNow(Valid() with { DayTemplateId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Day template id must not be empty.");
    }

    [Test]
    public void Validate_NoMemberOfStaff_Fails()
    {
        _validator.ValidateNow(Valid() with { StaffUserId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Member of staff is required.");
    }

    [Test]
    public void Validate_Saturday_Fails()
    {
        _validator.ValidateNow(Valid() with { Date = NextSaturday })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(WorkingHours.Weekend);
    }

    [Test]
    public void Validate_Sunday_Fails()
    {
        _validator.ValidateNow(Valid() with { Date = NextSunday })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(WorkingHours.Weekend);
    }

    [Test]
    public void Validate_OnAClosedDay_SaysTheClinicIsClosed()
    {
        _validator.ValidateNow(Valid() with { Date = ClosedWednesday })
            .Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
    }
}
