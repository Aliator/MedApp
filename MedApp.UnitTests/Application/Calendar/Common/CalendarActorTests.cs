using MedApp.UnitTests.Common.Constants;
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
        new CalendarActor(StaffUserId, StaffUsername, TestPermissions.Staff).Should().Be(StaffActor);
    }

    [Test]
    public void CanChangeCalendarOf_SomeoneElseWithManageAnyCalendar_IsTrue_WithoutAdmin()
    {
        var booking = StaffActor with { Permissions = MedApp.Application.Common.Identity.StandardRoles.Booking.Permissions };

        booking.CanChangeCalendarOf(OtherStaffUserId).Should().BeTrue();
        booking.CanArrangeMeetings.Should().BeFalse();
    }

    [Test]
    public void CanArrangeMeetings_WithArrangeMeetings_IsTrue()
    {
        var manager = StaffActor with { Permissions = MedApp.Application.Common.Identity.StandardRoles.Manager.Permissions };

        manager.CanArrangeMeetings.Should().BeTrue();
        manager.CanChangeCalendarOf(OtherStaffUserId).Should().BeFalse();
    }

    [Test]
    public void NoPermissions_OwnCalendarOnly()
    {
        var actor = new CalendarActor(StaffUserId, StaffUsername);

        actor.CanChangeCalendarOf(StaffUserId).Should().BeTrue();
        actor.CanChangeCalendarOf(OtherStaffUserId).Should().BeFalse();
        actor.Has(MedApp.Application.Common.Identity.Permission.UseApp).Should().BeFalse();
    }

    [Test]
    public void Equality_ComparesThePermissionsByContent()
    {
        var copy = new HashSet<MedApp.Application.Common.Identity.Permission>(TestPermissions.Staff);

        new CalendarActor(StaffUserId, StaffUsername, copy).Should().Be(StaffActor);
        new CalendarActor(StaffUserId, StaffUsername, copy).GetHashCode().Should().Be(StaffActor.GetHashCode());
        new CalendarActor(StaffUserId, StaffUsername, TestPermissions.Admin).Should().NotBe(StaffActor);
        new CalendarActor(StaffUserId, StaffUsername).Should().Be(new CalendarActor(StaffUserId, StaffUsername, new HashSet<MedApp.Application.Common.Identity.Permission>()));
        StaffActor.Equals(null).Should().BeFalse();
    }

    [Test]
    public void CanSetHours_WithManageClinicsOrManageAnyCalendar_Only()
    {
        StaffActor.CanSetHours.Should().BeFalse("nobody sets their own hours without one of the two");
        AdminActor.CanSetHours.Should().BeTrue();
        (StaffActor with { Permissions = new HashSet<MedApp.Application.Common.Identity.Permission> { MedApp.Application.Common.Identity.Permission.ManageClinics } })
            .CanSetHours.Should().BeTrue();
        (StaffActor with { Permissions = new HashSet<MedApp.Application.Common.Identity.Permission> { MedApp.Application.Common.Identity.Permission.ManageAnyCalendar } })
            .CanSetHours.Should().BeTrue();
    }
}
