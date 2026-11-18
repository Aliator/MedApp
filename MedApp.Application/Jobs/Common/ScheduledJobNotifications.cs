using System.Globalization;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Jobs.Common;

/// <summary>
/// The notification that a scheduled job keeps failing (<see cref="NotificationKind.ScheduledJobFailing"/>): a fixed
/// title, and a message with the job's name and the count, never the error.
/// </summary>
public static class ScheduledJobNotifications
{
    public const string FailingTitle = "Scheduled job failing";

    /// <param name="userIds">Who's told: the holders of ManageSetup.</param>
    public static IReadOnlyList<Notification> Failing(
        IEnumerable<Guid> userIds, string jobTitle, int failures, DateTime nowUtc)
    {
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"{jobTitle} has failed {failures} times in a row. Scheduled Jobs, under Administration, shows when it last ran; the API's log has the error.");

        return userIds
            .Distinct()
            .Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Kind = NotificationKind.ScheduledJobFailing,
                Title = FailingTitle,
                Message = message,
                CreatedAtUtc = nowUtc
            })
            .ToList();
    }
}
