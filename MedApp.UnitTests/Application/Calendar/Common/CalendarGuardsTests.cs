using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarGuardsTests
{
    [Test]
    public void EnsureCanChange_OwnCalendar_Passes()
    {
        var act = () => StaffActor.EnsureCanChange(StaffUserId);

        act.Should().NotThrow();
    }

    [Test]
    public void EnsureCanChange_AdminOnSomeoneElses_Passes()
    {
        var act = () => AdminActor.EnsureCanChange(StaffUserId);

        act.Should().NotThrow();
    }

    [Test]
    public void EnsureCanChange_StaffOnSomeoneElses_ThrowsForbidden()
    {
        var act = () => OtherStaffActor.EnsureCanChange(StaffUserId);

        act.Should().Throw<ForbiddenException>().WithMessage(CalendarActor.NotYourCalendar);
    }

    [Test]
    public void EnsureNotPast_Yesterday_ThrowsConflict()
    {
        var act = () => CalendarGuards.EnsureNotPast(Today.AddDays(-1), Today);

        act.Should().Throw<ConflictException>().WithMessage(CalendarGuards.PastDay);
    }

    [Test]
    public void EnsureNotPast_TodayOrLater_Passes()
    {
        var today = () => CalendarGuards.EnsureNotPast(Today, Today);
        var tomorrow = () => CalendarGuards.EnsureNotPast(Today.AddDays(1), Today);

        today.Should().NotThrow();
        tomorrow.Should().NotThrow();
    }
}
