using FluentAssertions;
using MedApp.Domain.Notifications;

namespace MedApp.UnitTests.Domain.Notifications;

[TestFixture]
public sealed class NotificationPreferencesTests
{
    private static readonly Guid UserId = Guid.Parse("0000000b-0000-0000-0000-000000000009");

    [Test]
    public void Defaults_WantEveryKind()
    {
        var defaults = NotificationPreferences.Defaults(UserId);

        defaults.UserId.Should().Be(UserId);
        Enum.GetValues<NotificationKind>().Should().OnlyContain(kind => defaults.Wants(kind));
    }

    [Test]
    public void Wants_AssignedTurnedOff_RefusesOnlyThatKind()
    {
        var preferences = new NotificationPreferences { UserId = UserId, PathwayAssigned = false };

        preferences.Wants(NotificationKind.PathwayAssigned).Should().BeFalse();
        preferences.Wants(NotificationKind.MonitoredStageFinished).Should().BeTrue();
    }

    [Test]
    public void HasFixedTitle_TheAppointmentMeetingMonitoringAndNoteDeletionKinds()
    {
        Enum.GetValues<NotificationKind>().Where(Notification.HasFixedTitle)
            .Should().BeEquivalentTo(Enum.GetValues<NotificationKind>()
                .Where(k => k.ToString().StartsWith("Monitored")
                            || k.ToString().StartsWith("Appointment")
                            || k.ToString().StartsWith("Meeting")
                            || k.ToString().StartsWith("NoteDeletion")
                            || k == NotificationKind.AuthorsNoteDeleted
                            || k == NotificationKind.DayClosed
                            || k == NotificationKind.StageVisitWithdrawn
                            || k == NotificationKind.ClinicSessionCancelled
                            || k == NotificationKind.ScheduledJobFailing));
        Notification.HasFixedTitle(NotificationKind.PathwayAssigned).Should().BeFalse();
    }

    [Test]
    public void IsMonitoring_NotTheMeetingKinds()
    {
        Notification.IsMonitoring(NotificationKind.MeetingInvited).Should().BeFalse();
        Notification.IsMonitoring(NotificationKind.MeetingCancelled).Should().BeFalse();
        Notification.IsMonitoring(NotificationKind.MeetingDeclined).Should().BeFalse();
        Notification.IsMonitoring(NotificationKind.MonitoredPathwayCreated).Should().BeTrue();
        Notification.IsMonitoring(NotificationKind.NoteDeletionRequested).Should().BeFalse();
    }

    [Test]
    public void IsNoteDeletion_OnlyTheNoteDeletionKinds()
    {
        Enum.GetValues<NotificationKind>().Where(Notification.IsNoteDeletion).Should().Equal(
            NotificationKind.NoteDeletionRequested, NotificationKind.NoteDeletionApproved, NotificationKind.NoteDeletionDeclined,
            NotificationKind.AuthorsNoteDeleted);
    }

    [TestCase(NotificationKind.NoteDeletionRequested, 18)]
    [TestCase(NotificationKind.NoteDeletionApproved, 19)]
    [TestCase(NotificationKind.NoteDeletionDeclined, 20)]
    [TestCase(NotificationKind.AuthorsNoteDeleted, 21)]
    [TestCase(NotificationKind.MeetingInvited, 4)]
    [TestCase(NotificationKind.MeetingChanged, 5)]
    [TestCase(NotificationKind.MeetingCancelled, 6)]
    [TestCase(NotificationKind.MeetingDeclined, 7)]
    public void MeetingKinds_KeepTheirStoredNumbers(NotificationKind kind, int number)
    {
        ((int)kind).Should().Be(number);
    }

    [Test]
    public void IsOptional_OnlyTheAssignedKind()
    {
        Enum.GetValues<NotificationKind>().Where(NotificationPreferences.IsOptional)
            .Should().Equal(NotificationKind.PathwayAssigned);
    }
}
