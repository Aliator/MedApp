using FluentAssertions;
using MedApp.Application.Clinics.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Common;

[TestFixture]
public sealed class ClinicMapperTests
{
    [Test]
    public void ToResponse_MapsEveryFieldWithItsRoomSiteAndSpecialty()
    {
        var session = CreateSession();

        var response = ClinicMapper.ToResponse(session, StaffUsername, [At(NextMonday, 9)], Now, canManage: true);

        response.Should().BeEquivalentTo(new
        {
            session.Id,
            StaffUserId,
            StaffUsername,
            RoomId = SitesTestConstants.RoomId,
            RoomName = SitesTestConstants.RoomName,
            SiteId = SitesTestConstants.SiteId,
            SiteName = SitesTestConstants.SiteName,
            SpecialtyId,
            SpecialtyName,
            Date = NextMonday,
            StartUtc = At(NextMonday, 9),
            EndUtc = At(NextMonday, 12),
            SlotMinutes = 20,
            SlotCount = 9,
            BookedCount = 1,
            FreeSlotCount = 8,
            Status = "Planned",
            FreeSlotStartsUtc = new[] { At(NextMonday, 9, 20), At(NextMonday, 9, 40), At(NextMonday, 10), At(NextMonday, 10, 20), At(NextMonday, 10, 40), At(NextMonday, 11), At(NextMonday, 11, 20), At(NextMonday, 11, 40) },
            CanChange = true
        });
    }

    [Test]
    public void ToResponse_CanChange_OnlyForAManagerOfAPlannedSessionNotPast()
    {
        ClinicMapper.ToResponse(CreateSession(), StaffUsername, [], Now, canManage: false).CanChange.Should().BeFalse();
        ClinicMapper.ToResponse(CreateSession(status: ClinicSessionStatus.Cancelled), StaffUsername, [], Now, true).CanChange.Should().BeFalse();
        ClinicMapper.ToResponse(CreateSession(), StaffUsername, [], At(NextMonday, 13).AddDays(1), true).CanChange.Should().BeFalse();
    }

    [Test]
    public void ToResponse_Cancelled_HasNoFreeSlots()
    {
        var session = CreateSession(status: ClinicSessionStatus.Cancelled);
        session.CancelReason = CancelReason;

        var response = ClinicMapper.ToResponse(session, StaffUsername, [], Now, true);

        response.FreeSlotCount.Should().Be(0);
        response.FreeSlotStartsUtc.Should().BeEmpty();
        response.CancelReason.Should().Be(CancelReason);
    }

    [Test]
    public void ToDetailResponse_EachSlotWithItsAppointment_AndWhetherItsPast()
    {
        var session = CreateSession();
        var attended = InSession(session, At(NextMonday, 9), AppointmentStatus.Attended);

        var detail = ClinicMapper.ToDetailResponse(session, StaffUsername, [attended], At(NextMonday, 9, 30), true);

        detail.Slots.Should().HaveCount(9);
        detail.Slots[0].Should().BeEquivalentTo(new { AppointmentId = attended.Id, attended.PatientId, AppointmentStatus = "Attended", IsPast = true });
        detail.Slots[1].IsPast.Should().BeTrue();
        detail.Slots[2].Should().BeEquivalentTo(new { AppointmentId = (Guid?)null, PatientName = (string?)null, IsPast = false });
        detail.Session.BookedCount.Should().Be(1);
    }

    [Test]
    public void ToDetailResponse_CancelledSession_ListsThoseCancelledWithItToTell_NotInItsSlots()
    {
        var session = CreateSession(status: ClinicSessionStatus.Cancelled);
        var cancelled = InSession(session, At(NextMonday, 9, 20));
        cancelled.Status = AppointmentStatus.Cancelled;
        var attended = InSession(session, At(NextMonday, 9), AppointmentStatus.Attended);

        var detail = ClinicMapper.ToDetailResponse(session, StaffUsername, [attended, cancelled], At(NextMonday, 9, 30), true);

        detail.PatientsToTell.Select(p => p.AppointmentId).Should().Equal(cancelled.Id);
        detail.Slots[0].AppointmentId.Should().Be(attended.Id);
        detail.Slots[1].AppointmentId.Should().BeNull();
        ClinicMapper.ToDetailResponse(CreateSession(), StaffUsername, [], Now, true).PatientsToTell.Should().BeEmpty();
    }

    [Test]
    public void ToPatientToTell_NamesThePatientAndTheTime()
    {
        var appointment = InSession(CreateSession(), At(NextMonday, 9, 40));

        var told = ClinicMapper.ToPatientToTell(appointment);

        told.Should().BeEquivalentTo(new
        {
            AppointmentId = appointment.Id,
            appointment.PatientId,
            PatientName = $"{PatientFirstName} {PatientLastName}",
            appointment.Patient.NhsNumber,
            StartUtc = At(NextMonday, 9, 40)
        });
    }

    [Test]
    public void ToResponse_Pattern_MapsEveryField()
    {
        var pattern = CreatePattern(everyWeeks: 2, untilDate: NextMonday);

        var response = ClinicMapper.ToResponse(pattern, StaffUsername, Today);

        response.Should().BeEquivalentTo(new
        {
            Id = PatternId,
            StaffUsername,
            RoomName = SitesTestConstants.RoomName,
            SiteName = SitesTestConstants.SiteName,
            SpecialtyName,
            DayOfWeek = "Monday",
            Starts = new TimeOnly(9, 0),
            Ends = new TimeOnly(12, 0),
            SlotMinutes = 20,
            EveryWeeks = 2,
            FromDate = Today,
            UntilDate = (DateOnly?)NextMonday,
            HasEnded = false
        });
    }

    [Test]
    public void ClinicNotifications_FixedTitleAndNoReasonOrPatient()
    {
        var session = CreateSession();
        session.CancelReason = CancelReason;

        var notification = ClinicNotifications.AfterCancelling(session, AdminActor, Now).Should().ContainSingle().Subject;

        notification.Kind.Should().Be(NotificationKind.ClinicSessionCancelled);
        notification.Title.Should().Be(ClinicNotifications.CancelledTitle);
        Notification.HasFixedTitle(notification.Kind).Should().BeTrue();
        notification.Message.Should().NotContain(CancelReason);
        ClinicNotifications.AfterCancelling(session, StaffActor, Now).Should().BeEmpty();
    }
}
