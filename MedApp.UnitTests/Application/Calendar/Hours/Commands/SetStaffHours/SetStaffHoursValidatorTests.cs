using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Hours.Commands.SetStaffHours;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Hours.Commands.SetStaffHours;

[TestFixture]
public sealed class SetStaffHoursValidatorTests
{
    private static readonly Guid SiteId = Guid.Parse("5173e000-0000-4000-8000-0000000000c1");
    private readonly SetStaffHoursValidator _validator = new();

    private static StaffHoursDay Day(string day = "Saturday", int starts = 9, int ends = 13, Guid? siteId = null, int endMinute = 0) =>
        new(day, new TimeOnly(starts, 0), new TimeOnly(ends, endMinute), siteId ?? SiteId);

    private IEnumerable<string> Errors(params StaffHoursDay[] days) =>
        _validator.Validate(new SetStaffHoursCommand(StaffUserId, days, AdminActor)).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_AnyDayOfTheWeek_InsideTheBounds_Passes()
    {
        Errors(Day("Monday", 6, 23), Day("saturday"), Day("Sunday", 18, 20)).Should().BeEmpty();
        Errors().Should().BeEmpty("no days is the default");
    }

    [Test]
    public void Validate_NoList_Fails()
    {
        _validator.Validate(new SetStaffHoursCommand(StaffUserId, null, AdminActor)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoStaff_Fails()
    {
        _validator.Validate(new SetStaffHoursCommand(Guid.Empty, [], AdminActor)).Errors.Should().ContainSingle();
    }

    [TestCase("Funday")]
    [TestCase("6")]
    [TestCase("")]
    public void Validate_NotADayOfTheWeek_Fails(string day)
    {
        Errors(Day(day)).Should().Equal(HoursValidation.DayOfWeekMessage);
    }

    [Test]
    public void Validate_ADayTwice_Fails()
    {
        Errors(Day("Monday"), Day("monday", 14, 17)).Should().Equal(HoursValidation.DayRepeated);
    }

    [Test]
    public void Validate_OutsideTheBoundsOrTheGrid_FailsSayingWhy()
    {
        Errors(Day(starts: 5)).Should().Equal(WorkingHours.OutsideBounds);
        Errors(Day(starts: 13, ends: 9)).Should().Equal(WorkingHours.EndNotAfterStart);
        Errors(Day(ends: 12, endMinute: 2)).Should().Equal(WorkingHours.NotOnBoundary);
    }

    [Test]
    public void Validate_NoSite_Fails()
    {
        Errors(Day(siteId: Guid.Empty)).Should().Equal(SetStaffHoursValidator.NoSite);
    }
}
