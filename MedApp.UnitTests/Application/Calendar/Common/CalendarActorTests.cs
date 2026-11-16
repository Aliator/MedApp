using FluentAssertions;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarActorTests
{
    [Test]
    public void CanChangeCalendarOf_Themselves_IsTrue()
    {
        StaffActor.CanChangeCalendarOf(StaffUserId).Should().BeTrue();
    }

    [Test]
    public void CanChangeCalendarOf_SomeoneElseAsStaff_IsFalse()
    {
        StaffActor.CanChangeCalendarOf(OtherStaffUserId).Should().BeFalse();
    }

    [Test]
    public void CanChangeCalendarOf_SomeoneElseAsAdmin_IsTrue()
    {
        AdminActor.CanChangeCalendarOf(StaffUserId).Should().BeTrue();
        AdminActor.CanChangeCalendarOf(Guid.NewGuid()).Should().BeTrue();
    }

    [Test]
    public void Equality_SameValues_AreEqual()
    {
        new CalendarActor(StaffUserId, StaffUsername, false).Should().Be(StaffActor);
    }
}
