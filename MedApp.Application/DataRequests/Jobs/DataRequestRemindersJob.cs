using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Application.Jobs.Common;
using MedApp.Domain.DataRequests;
using MedApp.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace MedApp.Application.DataRequests.Jobs;

/// <summary>
/// Data request reminders (decision 160), daily at 07:00 UK time: tells every holder of ManagePrivacy about each open
/// data request due within <see cref="DataRequestRules.DueSoonDays"/> days (or already overdue) that they haven't been
/// told about for its current due date (<see cref="DataRequest.DueSoonNotifiedFor"/>). The notifications and the mark
/// are saved together, so a run cut short repeats nothing. Logs counts only.
/// </summary>
public sealed class DataRequestRemindersJob(
    IDataRequestRepository repository,
    IPermissionHolders permissionHolders,
    ILogger<DataRequestRemindersJob> logger) : IScheduledJob
{
    public const string JobName = "data-request-reminders";

    /// <summary>The most requests one run handles; the rest wait for the next run.</summary>
    public const int BatchSize = 200;

    public string Name => JobName;

    public string Title => "Data request reminders";

    public string Description =>
        $"Tells the holders of ManagePrivacy about each open data request due within {DataRequestRules.DueSoonDays} days, once for each due date.";

    public JobSchedule Schedule { get; } = JobSchedule.DailyAt(new TimeOnly(7, 0));

    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var today = UkTime.DateOf(nowUtc);
        var due = await repository.GetDueSoonUnnotifiedAsync(today.AddDays(DataRequestRules.DueSoonDays), BatchSize, cancellationToken);
        if (due.Count == 0)
            return 0;

        var holders = await permissionHolders.GetUserIdsAsync(Permission.ManagePrivacy, cancellationToken);
        var notifications = new List<Notification>();
        foreach (var request in due)
        {
            notifications.AddRange(DataRequestNotifications.DueSoon(holders, request, nowUtc));
            request.DueSoonNotifiedFor = request.DueOn;
        }

        await repository.SaveRemindersAsync(notifications, cancellationToken);
        logger.LogInformation("Data request reminders: {Requests} requests due soon, {Notifications} notifications.", due.Count, notifications.Count);
        return due.Count;
    }
}
