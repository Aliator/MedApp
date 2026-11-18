using FluentAssertions;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;
using MedApp.Domain.Sites;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Common;

[TestFixture]
public sealed class ClinicRulesTests
{
    [TestCase(20, null)]
    [TestCase(5, null)]
    [TestCase(120, "divide")]
    [TestCase(45, "divide")]
    [TestCase(4, "slot")]
    [TestCase(121, "slot")]
    [TestCase(22, "slot")]
    public void WhyBadSlots_ChecksTheLengthAndThatSlotsDivideTheSession(int slotMinutes, string? expected)
    {
        // Three hours, or three hours ten minutes for the cases that shouldn't divide.
        var end = expected == "divide" ? At(NextMonday, 12, 10) : At(NextMonday, 12);

        var why = ClinicRules.WhyBadSlots(At(NextMonday, 9), end, slotMinutes);

        if (expected is null)
            why.Should().BeNull();
        else if (expected == "slot")
            why.Should().Be(ClinicRules.SlotLength);
        else
            why.Should().Be(ClinicRules.SlotsDontDivide);
    }

    [Test]
    public void WhyBadSlots_120MinuteSlotsOverTwoHours_Pass() =>
        ClinicRules.WhyBadSlots(At(NextMonday, 9), At(NextMonday, 11), 120).Should().BeNull();

    [Test]
    public void Slots_CutTheSessionIntoItsSlotLength()
    {
        var slots = ClinicRules.Slots(CreateSession());

        slots.Should().HaveCount(9);
        slots[0].Should().Be((At(NextMonday, 9), At(NextMonday, 9, 20)));
        slots[^1].Should().Be((At(NextMonday, 11, 40), At(NextMonday, 12)));
    }

    [Test]
    public void Slots_NoSlotLength_None() => ClinicRules.Slots(CreateSession(slotMinutes: 0)).Should().BeEmpty();

    [Test]
    public void IsSlot_OnlyExactSlots()
    {
        var session = CreateSession();

        ClinicRules.IsSlot(session, At(NextMonday, 9, 20), At(NextMonday, 9, 40)).Should().BeTrue();
        ClinicRules.IsSlot(session, At(NextMonday, 9, 10), At(NextMonday, 9, 30)).Should().BeFalse();
        ClinicRules.IsSlot(session, At(NextMonday, 9, 20), At(NextMonday, 10)).Should().BeFalse();
        ClinicRules.IsSlot(session, At(NextMonday, 12), At(NextMonday, 12, 20)).Should().BeFalse();
    }

    [Test]
    public void Counts_BookedSlots_AndFreeOnesNotStarted()
    {
        var session = CreateSession();

        var (booked, free) = ClinicRules.Counts(session, [At(NextMonday, 9), At(NextMonday, 10)], At(NextMonday, 9, 30));

        booked.Should().Be(2);
        // Nine slots: two booked, and of the other seven, 09:20 has started.
        free.Should().Be(6);
    }

    [Test]
    public void WhyRoomUnavailable_FreeRoom_Null() =>
        ClinicRules.WhyRoomUnavailable(CreateRoomDay(), At(NextMonday, 9), At(NextMonday, 12)).Should().BeNull();

    [Test]
    public void WhyRoomUnavailable_SiteClosedThatDay_SaysSo()
    {
        var closed = SiteClosedDay(SitesTestConstantsSiteId, "Synthetic site closure");

        var why = ClinicRules.WhyRoomUnavailable(CreateRoomDay(closedDays: [closed]), At(NextMonday, 9), At(NextMonday, 12));

        why.Should().Contain("is closed on Monday 23 November (Synthetic site closure)").And.Contain("Room 2");
    }

    [Test]
    public void WhyRoomUnavailable_AnotherSitesClosedDay_DoesNotCount()
    {
        var closed = SiteClosedDay(Guid.NewGuid(), ClosedDayName);

        ClinicRules.WhyRoomUnavailable(CreateRoomDay(closedDays: [closed]), At(NextMonday, 9), At(NextMonday, 12)).Should().BeNull();
    }

    [Test]
    public void WhyRoomUnavailable_OutsideTheSitesHours_SaysWhenItsOpen()
    {
        var hours = new SiteHours { Id = Guid.NewGuid(), SiteId = SitesTestConstantsSiteId, DayOfWeek = DayOfWeek.Monday, Opens = new TimeOnly(10, 0), Closes = new TimeOnly(16, 0), ValidFrom = LongAgo };

        var why = ClinicRules.WhyRoomUnavailable(CreateRoomDay(siteHours: [hours]), At(NextMonday, 9), At(NextMonday, 12));

        why.Should().EndWith("is open 10:00 to 16:00 on Mondays.");
    }

    [Test]
    public void WhyRoomUnavailable_SiteNotOpenThatWeekday_SaysSo()
    {
        var hours = new SiteHours { Id = Guid.NewGuid(), SiteId = SitesTestConstantsSiteId, DayOfWeek = DayOfWeek.Tuesday, Opens = new TimeOnly(8, 0), Closes = new TimeOnly(18, 0), ValidFrom = LongAgo };

        var why = ClinicRules.WhyRoomUnavailable(CreateRoomDay(siteHours: [hours]), At(NextMonday, 9), At(NextMonday, 12));

        why.Should().Contain("isn't open on Mondays");
    }

    [Test]
    public void WhyRoomUnavailable_AnotherSessionInTheRoom_NamesIt()
    {
        var other = CreateSession(At(NextMonday, 11), At(NextMonday, 13), id: OtherSessionId);

        var why = ClinicRules.WhyRoomUnavailable(CreateRoomDay(sessions: [other]), At(NextMonday, 9), At(NextMonday, 12));

        why.Should().Be("Room 2 is taken by a clinic session, 11:00 to 13:00.");
    }

    [Test]
    public void WhyRoomUnavailable_TheSessionBeingChangedAndItsAppointments_AreLeftOut()
    {
        var session = CreateSession();
        var booked = InSession(session, At(NextMonday, 9));

        ClinicRules.WhyRoomUnavailable(
                CreateRoomDay(sessions: [session], appointments: [booked]), At(NextMonday, 9), At(NextMonday, 12), ignoreSessionId: session.Id)
            .Should().BeNull();
    }

    [Test]
    public void WhyRoomUnavailable_AnAppointmentInTheRoom_NamesNoPatient()
    {
        var appointment = CreateAppointment(startUtc: At(NextMonday, 10));
        appointment.RoomId = SitesTestConstantsRoomId;

        ClinicRules.WhyRoomUnavailable(CreateRoomDay(appointments: [appointment]), At(NextMonday, 9), At(NextMonday, 12))
            .Should().Be("Room 2 is taken by another appointment, 10:00 to 10:30.");
        ClinicRules.WhyRoomUnavailable(CreateRoomDay(appointments: [appointment]), At(NextMonday, 9), At(NextMonday, 12), ignoreAppointmentId: appointment.Id)
            .Should().BeNull();
    }

    [Test]
    public void WhyRoomUnavailable_CancelledSessionsAndAppointments_DoNotCount()
    {
        var cancelled = CreateSession(status: ClinicSessionStatus.Cancelled, id: OtherSessionId);
        var appointment = CreateAppointment(AppointmentStatus.Cancelled, At(NextMonday, 10));

        ClinicRules.WhyRoomUnavailable(CreateRoomDay(sessions: [cancelled], appointments: [appointment]), At(NextMonday, 9), At(NextMonday, 12))
            .Should().BeNull();
    }

    [Test]
    public void EnsureUnchanged_Stale_Is409()
    {
        var session = CreateSession();

        FluentActions.Invoking(() => ClinicRules.EnsureUnchanged(session, session.LastUpdated.AddSeconds(-1)))
            .Should().Throw<ConflictException>().WithMessage(ClinicRules.ChangedMeanwhile);
        FluentActions.Invoking(() => ClinicRules.EnsureUnchanged(session, session.LastUpdated)).Should().NotThrow();
    }

    [Test]
    public void EnsurePlanned_Cancelled_Is409() =>
        FluentActions.Invoking(() => ClinicRules.EnsurePlanned(CreateSession(status: ClinicSessionStatus.Cancelled)))
            .Should().Throw<ConflictException>().WithMessage(ClinicRules.AlreadyCancelled);

    [Test]
    public void NotASlot_SaysTheSlotLengthAndStart() =>
        ClinicRules.NotASlot(CreateSession()).Should().Be("Choose one of the session's 20-minute slots, from 09:00.");

    [Test]
    public void RoomArchived_NamesTheRoom() =>
        ClinicRules.RoomArchived(Room()).Should().Be("Room 2 is archived, so it can't be chosen for anything new.");

    private static MedApp.Domain.Calendar.ClosedDay SiteClosedDay(Guid siteId, string name) => new()
    {
        Id = Guid.NewGuid(),
        Date = NextMonday,
        Name = name,
        SiteId = siteId,
        CreatedAtUtc = Now,
        CreatedByUserId = AdminUserId
    };

    private static Guid SitesTestConstantsSiteId => SitesTestConstants.SiteId;

    private static Guid SitesTestConstantsRoomId => SitesTestConstants.RoomId;
}
