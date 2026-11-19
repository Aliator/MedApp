using MedApp.Application.Jobs.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Domain.Mdt;
using MedApp.Domain.Teams;
using Microsoft.Extensions.Logging;

namespace MedApp.Application.Mdt.Common;

/// <summary>
/// MDT attendees' grants (MC21 and MC26 of <c>docs/multi-clinic-plan.md</c>, <see cref="PathwayAccessReason.Mdt"/>; the
/// light demo's <c>mdtGrantedItems</c>): each attendee of an MDT, from whichever team, reads each pathway listed on it,
/// by person and read only, from its listing until the item is discussed or withdrawn, and at least to the meeting's
/// end. Only the listed cases. A grant follows its item's current MDT (a move gives the new MDT's attendees theirs and
/// ends the others') and the meeting's attendees as they are now. Written in the same save as the change that causes
/// it (listing, a move, a withdrawal, an outcome, a change of attendees, a cancelled MDT); those that lapse when a
/// meeting ends are ended by <see cref="MdtGrantsJob"/>.
/// </summary>
public static class MdtGrants
{
    /// <summary>Who should hold a live grant for the item now: its MDT's attendees while it's open or the meeting hasn't ended.</summary>
    public static IReadOnlySet<Guid> Holders(MdtItem item, DateTime nowUtc) =>
        item.MeetingId is not null
        && item.Meeting is { IsMdt: true } meeting
        && (item.IsOpen || meeting.EndUtc > nowUtc)
            ? item.Meeting.Attendees.Select(a => a.StaffUserId).ToHashSet()
            : new HashSet<Guid>();

    /// <summary>
    /// Brings the item's grants in line with <see cref="Holders"/>: ends the live ones of anyone who shouldn't hold one
    /// (in <paramref name="live"/>, tracked) and returns the new ones to add.
    /// </summary>
    public static IReadOnlyList<PathwayAccess> Sync(MdtItem item, IEnumerable<PathwayAccess> live, DateTime nowUtc)
    {
        var holders = Holders(item, nowUtc);
        var current = live
            .Where(a => a.Reason == PathwayAccessReason.Mdt && a.MdtItemId == item.Id && a.EndedAtUtc is null)
            .ToList();

        foreach (var grant in current.Where(g => g.UserId is not { } userId || !holders.Contains(userId)))
            grant.EndedAtUtc = nowUtc;

        return holders
            .Where(userId => current.All(g => g.UserId != userId))
            .OrderBy(userId => userId)
            .Select(userId => PathwayAccess.Mdt(item.PathwayId, item.PatientId, userId, item.Id, nowUtc))
            .ToList();
    }
}

/// <summary>
/// Ends MDT grants that have lapsed (<c>mdt-grants</c>, every 15 minutes): an item discussed or withdrawn whose meeting
/// has ended, an item whose MDT was cancelled, or an attendee no longer invited. One UPDATE, so a run cut short repeats
/// nothing. Logs counts only.
/// </summary>
public sealed class MdtGrantsJob(IMdtItemRepository repository, ILogger<MdtGrantsJob> logger) : IScheduledJob
{
    public const string JobName = "mdt-grants";

    public string Name => JobName;

    public string Title => "MDT attendees' access";

    public string Description =>
        "Ends MDT attendees' reading of the cases listed once each is discussed or withdrawn and its meeting has ended.";

    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(15));

    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var ended = await repository.EndLapsedGrantsAsync(nowUtc, cancellationToken);
        if (ended > 0)
            logger.LogInformation("MDT grants: {Ended} ended.", ended);
        return ended;
    }
}
