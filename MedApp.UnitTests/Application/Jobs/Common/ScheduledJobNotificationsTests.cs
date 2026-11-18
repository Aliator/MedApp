using FluentAssertions;
using MedApp.Application.Jobs.Common;
using MedApp.Domain.Notifications;
using static MedApp.UnitTests.Common.Constants.JobsTestConstants;

namespace MedApp.UnitTests.Application.Jobs.Common;

[TestFixture]
public sealed class ScheduledJobNotificationsTests
{
    [Test]
    public void Failing_OneForEachAdmin_WithAFixedTitleAndTheCount()
    {
        var notifications = ScheduledJobNotifications.Failing([AdminId, OtherAdminId, AdminId], JobTitle, 3, Now);

        notifications.Select(n => n.UserId).Should().Equal(AdminId, OtherAdminId);
        notifications.Should().OnlyContain(n =>
            n.Kind == NotificationKind.ScheduledJobFailing
            && n.Title == "Scheduled job failing"
            && n.CreatedAtUtc == Now
            && n.PathwayId == null
            && n.Id != Guid.Empty);
        notifications[0].Message.Should().StartWith("Test job has failed 3 times in a row.");
    }

    [Test]
    public void ScheduledJobFailing_HasAFixedTitle()
    {
        Notification.HasFixedTitle(NotificationKind.ScheduledJobFailing).Should().BeTrue();
        Notification.IsMonitoring(NotificationKind.ScheduledJobFailing).Should().BeFalse();
        ((int)NotificationKind.ScheduledJobFailing).Should().Be(40);
    }
}
