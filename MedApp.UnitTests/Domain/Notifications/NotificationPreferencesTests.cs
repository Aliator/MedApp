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
        var preferences = new NotificationPreferences { UserId = UserId, ProcedureAssigned = false };

        preferences.Wants(NotificationKind.ProcedureAssigned).Should().BeFalse();
        preferences.Wants(NotificationKind.MonitoredStageFinished).Should().BeTrue();
    }

    [Test]
    public void HasFixedTitle_OnlyTheMonitoringKinds()
    {
        Enum.GetValues<NotificationKind>().Where(Notification.HasFixedTitle)
            .Should().BeEquivalentTo(Enum.GetValues<NotificationKind>().Where(k => k.ToString().StartsWith("Monitored")));
        Notification.HasFixedTitle(NotificationKind.ProcedureAssigned).Should().BeFalse();
    }

    [Test]
    public void IsOptional_OnlyTheAssignedKind()
    {
        Enum.GetValues<NotificationKind>().Where(NotificationPreferences.IsOptional)
            .Should().Equal(NotificationKind.ProcedureAssigned);
    }
}
