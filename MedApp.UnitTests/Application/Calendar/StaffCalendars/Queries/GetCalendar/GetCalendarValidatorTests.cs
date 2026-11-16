using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetCalendar;

[TestFixture]
public sealed class GetCalendarValidatorTests
{
    private readonly GetCalendarValidator _validator = new();

    private static GetCalendarQuery Valid() => new(null, NextMonday, NextSunday, StaffActor);

    [Test]
    public void Validate_AWeek_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_OneDay_Passes()
    {
        _validator.Validate(Valid() with { To = NextMonday }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TheMostDays_Passes()
    {
        _validator.Validate(Valid() with { To = NextMonday.AddDays(WorkingHours.MaxDays - 1) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TooManyDays_Fails()
    {
        _validator.Validate(Valid() with { To = NextMonday.AddDays(WorkingHours.MaxDays) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("A calendar shows at most 31 days.");
    }

    [Test]
    public void Validate_EndBeforeStart_Fails()
    {
        _validator.Validate(Valid() with { To = NextMonday.AddDays(-1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("The end of the range must not be before its start.");
    }

    [Test]
    public void Validate_NoFrom_Fails()
    {
        _validator.Validate(Valid() with { From = default })
            .Errors.Should().Contain(e => e.ErrorMessage == "From is required.");
    }

    [Test]
    public void Validate_NoTo_FailsOnceOnly()
    {
        _validator.Validate(Valid() with { To = default })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("To is required.");
    }
}
