using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class AppointmentNotificationsTests
{
    private static Appointment BookedAt(DateTime startUtc, Guid? staffUserId = null)
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, startUtc);
        appointment.StaffUserId = staffUserId ?? StaffUserId;
        appointment.Notes = Notes;
        return appointment;
    }

    [Test]
    public void AfterBooking_RequestedBookedBySomeoneElse_TellsTheStaffMemberItWasBooked()
    {
        var appointment = BookedAt(At(NextMonday, 10));

        var notification = AppointmentNotifications.AfterBooking(
                appointment, AppointmentStatus.Requested, null, null, Colleague, Now)
            .Should().ContainSingle().Subject;

        notification.Id.Should().NotBeEmpty();
        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentBooked);
        notification.Title.Should().Be("Appointment booked");
        notification.Message.Should().Be("calendar.colleague booked you an appointment on Monday 23 November at 10:00.");
        notification.PathwayId.Should().Be(appointment.PathwayId);
        notification.AppointmentId.Should().Be(appointment.Id);
        notification.CreatedAtUtc.Should().Be(Now);
        notification.ReadAtUtc.Should().BeNull();
    }

    [Test]
    public void AfterBooking_InSummer_DescribesTheUkTime()
    {
        // 09:00 UTC on a BST day is 10:00 UK time.
        var appointment = BookedAt(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc));

        AppointmentNotifications.AfterBooking(appointment, AppointmentStatus.Requested, null, null, Colleague, Now)
            .Single().Message.Should().EndWith("on Monday 15 June at 10:00.");
    }

    [TestCase(NotificationKind.AppointmentBooked, "Appointment booked")]
    [TestCase(NotificationKind.AppointmentRescheduled, "Appointment moved")]
    [TestCase(NotificationKind.AppointmentCancelled, "Appointment cancelled")]
    [TestCase(NotificationKind.AppointmentRecorded, "Appointment recorded")]
    [TestCase(NotificationKind.StageVisitWithdrawn, "Stage visit withdrawn")]
    public void TitleOf_EachAppointmentKind_IsAFixedPhrase(NotificationKind kind, string title)
    {
        AppointmentNotifications.TitleOf(kind).Should().Be(title);
        Notification.HasFixedTitle(kind).Should().BeTrue();
    }

    [Test]
    public void TitleOf_AnotherKind_Throws()
    {
        var act = () => AppointmentNotifications.TitleOf(NotificationKind.PathwayAssigned);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void AfterBooking_ForAPathway_IsTitledByTheKindNotThePathway()
    {
        var appointment = BookedAt(At(NextMonday, 10));
        var pathway = LinkToPathway(appointment);

        var notification = AppointmentNotifications.AfterBooking(appointment, AppointmentStatus.Requested, null, null, Colleague, Now)
            .Single();

        notification.Title.Should().Be("Appointment booked");
        notification.PathwayId.Should().Be(pathway.Id);
    }

    [Test]
    public void AfterBooking_BookedByThemselves_TellsNobody()
    {
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 10)), AppointmentStatus.Requested, null, null, Self, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterBooking_NotBooked_TellsNobody()
    {
        AppointmentNotifications.AfterBooking(
                CreateAppointment(AppointmentStatus.Requested), AppointmentStatus.Requested, null, null, Colleague, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterBooking_MovedToAnotherTime_TellsTheStaffMemberItMoved()
    {
        var appointment = BookedAt(At(NextMonday, 15));

        var notification = AppointmentNotifications.AfterBooking(
                appointment, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Colleague, Now)
            .Should().ContainSingle().Subject;

        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentRescheduled);
        notification.Title.Should().Be("Appointment moved");
        notification.Message.Should().Be("calendar.colleague moved your appointment to Monday 23 November at 15:00.");
    }

    [Test]
    public void AfterBooking_SameStaffAndStart_TellsNobody()
    {
        // Only the end moved: the start, which the message gives, is the same.
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 10)), AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Colleague, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterBooking_MovedByThemselves_TellsNobody()
    {
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 15)), AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Self, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterBooking_MovedToSomeoneElse_TellsTheNewOneItWasBookedAndTheOldOneItWent()
    {
        var appointment = BookedAt(At(NextMonday, 15), OtherStaffUserId);

        var notifications = AppointmentNotifications.AfterBooking(
            appointment, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Colleague, Now);

        notifications.Should().HaveCount(2);
        var booked = notifications.Single(n => n.UserId == OtherStaffUserId);
        booked.Kind.Should().Be(NotificationKind.AppointmentBooked);
        booked.Title.Should().Be("Appointment booked");
        booked.Message.Should().Be("calendar.colleague moved an appointment to you, on Monday 23 November at 15:00.");
        var cancelled = notifications.Single(n => n.UserId == StaffUserId);
        cancelled.Kind.Should().Be(NotificationKind.AppointmentCancelled);
        cancelled.Title.Should().Be("Appointment cancelled");
        cancelled.Message.Should().Be("calendar.colleague moved your appointment on Monday 23 November at 10:00 to someone else.");
    }

    [Test]
    public void AfterBooking_MovedByTheOldStaffMemberToSomeoneElse_TellsOnlyTheNewOne()
    {
        var appointment = BookedAt(At(NextMonday, 15), OtherStaffUserId);

        AppointmentNotifications.AfterBooking(
                appointment, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Self, Now)
            .Should().ContainSingle().Which.UserId.Should().Be(OtherStaffUserId);
    }

    [Test]
    public void AfterBooking_MovedByTheNewStaffMemberToThemselves_TellsOnlyTheOldOne()
    {
        var appointment = BookedAt(At(NextMonday, 15), StaffUserId);

        var notification = AppointmentNotifications.AfterBooking(
                appointment, AppointmentStatus.Booked, OtherStaffUserId, At(NextMonday, 10), Self, Now)
            .Should().ContainSingle().Subject;

        notification.UserId.Should().Be(OtherStaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentCancelled);
    }

    [Test]
    public void AfterBooking_PreviousStaffUnknown_TellsOnlyTheNewOne()
    {
        AppointmentNotifications.AfterBooking(
                BookedAt(At(NextMonday, 15)), AppointmentStatus.Booked, null, null, Colleague, Now)
            .Should().ContainSingle().Which.Kind.Should().Be(NotificationKind.AppointmentBooked);
    }

    [Test]
    public void AfterBooking_Notifications_NeverHoldThePatientsNameNotesOrPathwayTitle()
    {
        var appointment = BookedAt(At(NextMonday, 15), OtherStaffUserId);
        LinkToPathway(appointment);

        var notifications = AppointmentNotifications.AfterBooking(
            appointment, AppointmentStatus.Booked, StaffUserId, At(NextMonday, 10), Colleague, Now);

        notifications.Should().NotBeEmpty();
        foreach (var notification in notifications)
        {
            notification.Message.Should().NotContain(PatientFirstName).And.NotContain(PatientLastName).And.NotContain(Notes)
                .And.NotContain(PathwayTitle);
            notification.Title.Should().NotContain(PatientFirstName).And.NotContain(Notes).And.NotContain(PathwayTitle);
        }
    }

    [Test]
    public void AfterCancelling_BookedWithSomeoneElse_TellsThem()
    {
        var appointment = BookedAt(At(NextMonday, 10));
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelReason = CancelReason;

        var notification = AppointmentNotifications.AfterCancelling(appointment, AppointmentStatus.Booked, Colleague, Now)
            .Should().ContainSingle().Subject;

        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentCancelled);
        notification.Title.Should().Be("Appointment cancelled");
        notification.AppointmentId.Should().Be(appointment.Id);
        notification.Message.Should().Be("calendar.colleague cancelled your appointment on Monday 23 November at 10:00.");
        notification.Message.Should().NotContain(CancelReason).And.NotContain(PatientFirstName);
    }

    [Test]
    public void AfterCancelling_TheirOwn_TellsNobody()
    {
        AppointmentNotifications.AfterCancelling(BookedAt(At(NextMonday, 10)), AppointmentStatus.Booked, Self, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterCancelling_WasRequested_TellsNobody()
    {
        AppointmentNotifications.AfterCancelling(
                CreateAppointment(AppointmentStatus.Requested), AppointmentStatus.Requested, Colleague, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterCancelling_BookedWithNoTime_TellsNobody()
    {
        var appointment = BookedAt(At(NextMonday, 10));
        appointment.StartUtc = null;

        AppointmentNotifications.AfterCancelling(appointment, AppointmentStatus.Booked, Colleague, Now)
            .Should().BeEmpty();
    }

    // After recording one after the event

    private static Appointment Recorded(AppointmentStatus outcome, Guid? staffUserId = null)
    {
        var appointment = BookedAt(At(Today.AddDays(-3), 10), staffUserId);
        appointment.Status = outcome;
        appointment.IsRecordedAfterEvent = true;
        return appointment;
    }

    [TestCase(AppointmentStatus.Attended, "attended")]
    [TestCase(AppointmentStatus.DidNotAttend, "did not attend")]
    public void AfterRecording_BySomeoneElse_TellsTheStaffMemberWhenAndTheOutcome(AppointmentStatus outcome, string words)
    {
        var appointment = Recorded(outcome);

        var notification = AppointmentNotifications.AfterRecording(appointment, Colleague, Now).Should().ContainSingle().Subject;

        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentRecorded);
        notification.Title.Should().Be("Appointment recorded");
        notification.Message.Should().Be($"calendar.colleague recorded an appointment you had on Friday 13 November at 10:00, as {words}.");
        notification.AppointmentId.Should().Be(appointment.Id);
        notification.PathwayId.Should().Be(appointment.PathwayId);
        notification.CreatedAtUtc.Should().Be(Now);
        notification.Message.Should().NotContain(Notes).And.NotContain(PatientLastName);
    }

    [Test]
    public void AfterRecording_ByTheStaffMemberThemselves_TellsNobody()
    {
        AppointmentNotifications.AfterRecording(Recorded(AppointmentStatus.Attended), Self, Now).Should().BeEmpty();
    }

    [Test]
    public void AfterRecording_NotAnOutcome_TellsNobody()
    {
        AppointmentNotifications.AfterRecording(BookedAt(At(NextMonday, 10)), Colleague, Now).Should().BeEmpty();
    }

    /// <summary>A walk-in for a finished stage, withdrawn, on a pathway assigned to the member of staff.</summary>
    private static (Appointment Withdrawn, PatientPathway Pathway, PatientPathwayStage Stage) WithdrawnFromFinishedStage()
    {
        var withdrawn = Recorded(AppointmentStatus.Attended);
        withdrawn.Status = AppointmentStatus.EnteredInError;
        var pathway = LinkToPathway(withdrawn);
        var stage = pathway.StageAt(1);
        stage.IsCompleted = true;
        stage.CompletedAtUtc = At(Today.AddDays(-3), 15);
        pathway.Assignments.Clear();
        pathway.Assignments.Add(new PatientPathwayAssignment { PatientPathwayId = pathway.Id, UserId = StaffUserId });
        return (withdrawn, pathway, stage);
    }

    private static readonly Guid OtherAdmin = Guid.Parse("0000000b-0000-0000-0000-0000000000ad");

    [Test]
    public void AfterStageVisitWithdrawn_TellsTheAssigneesAndAdmins_ButNotWhoWithdrewIt()
    {
        var (withdrawn, pathway, stage) = WithdrawnFromFinishedStage();

        var notifications = AppointmentNotifications.AfterStageVisitWithdrawn(
            withdrawn, pathway, stage, [OtherAdmin, Colleague.UserId, StaffUserId], Colleague, Now);

        notifications.Select(n => n.UserId).Should().Equal(StaffUserId, OtherAdmin);
        notifications.Should().AllSatisfy(n =>
        {
            n.Kind.Should().Be(NotificationKind.StageVisitWithdrawn);
            n.Title.Should().Be("Stage visit withdrawn");
            n.Message.Should().Be(
                $"calendar.colleague marked the visit on Friday 13 November at 10:00 for the '{StageName}' stage entered in error. " +
                "The stage was finished on Friday 13 November and now has no attended visit, though it requires one. " +
                "If it still needs its visit, an admin can unlock it, and then book one.");
            n.PathwayId.Should().Be(pathway.Id);
            n.AppointmentId.Should().BeNull("the appointment is off the calendar; the link is to the pathway");
            n.CreatedAtUtc.Should().Be(Now);
            n.Message.Should().NotContain(PathwayTitle).And.NotContain(Notes).And.NotContain(PatientLastName);
        });
    }

    [Test]
    public void AfterStageVisitWithdrawn_WithNoTimes_SaysAVisit()
    {
        var (withdrawn, pathway, stage) = WithdrawnFromFinishedStage();
        withdrawn.StartUtc = null;
        stage.CompletedAtUtc = null;

        AppointmentNotifications.AfterStageVisitWithdrawn(withdrawn, pathway, stage, [], Colleague, Now)
            .Should().ContainSingle().Which.Message.Should().StartWith(
                $"calendar.colleague marked a visit for the '{StageName}' stage entered in error. The stage was finished and now");
    }

    [Test]
    public void HasFixedTitle_StageVisitWithdrawn_NotMonitoring()
    {
        Notification.HasFixedTitle(NotificationKind.StageVisitWithdrawn).Should().BeTrue();
        Notification.IsMonitoring(NotificationKind.StageVisitWithdrawn).Should().BeFalse();
        ((int)NotificationKind.StageVisitWithdrawn).Should().Be(22);
    }

    [Test]
    public void HasFixedTitle_EveryCalendarKind_AndNotThePathwayAssignment()
    {
        Notification.HasFixedTitle(NotificationKind.AppointmentRecorded).Should().BeTrue();
        Notification.HasFixedTitle((NotificationKind)7).Should().BeTrue("7 and 8 are kept for calendar kinds, which have fixed titles");
        Notification.HasFixedTitle(NotificationKind.PathwayAssigned).Should().BeFalse();
        Notification.IsMonitoring(NotificationKind.AppointmentRecorded).Should().BeFalse();
    }
}
