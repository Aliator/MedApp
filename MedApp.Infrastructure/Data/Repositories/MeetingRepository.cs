using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Meetings. Changes run inside <c>CalendarLock</c>'s transaction, which these join (the request's DbContext).</summary>
public sealed class MeetingRepository(MedAppDbContext context) : IMeetingRepository
{
    public async Task<Meeting?> GetAsync(Guid id, CancellationToken ct) =>
        await context.Meetings.AsNoTracking().Include(m => m.Attendees).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Meeting?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.Meetings.Include(m => m.Attendees).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task AddAsync(Meeting meeting, CancellationToken ct)
    {
        context.Meetings.Add(meeting);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct) =>
        await context.SaveChangesOrConflictAsync(MeetingLimits.ChangedMeanwhile, ct);

    public async Task DeleteAsync(Meeting meeting, CancellationToken ct)
    {
        context.Meetings.Remove(meeting);
        await context.SaveChangesOrConflictAsync(MeetingLimits.ChangedMeanwhile, ct);
    }

    public async Task<int> RemoveAttendeeFromUpcomingAsync(Guid staffUserId, DateTime nowUtc, CancellationToken ct)
    {
        var meetings = await context.Meetings
            .Include(m => m.Attendees)
            .Where(m => m.StartUtc > nowUtc && m.Attendees.Any(a => a.StaffUserId == staffUserId))
            .ToListAsync(ct);

        foreach (var meeting in meetings)
        {
            meeting.Attendees.RemoveAll(a => a.StaffUserId == staffUserId);
            meeting.LastUpdated = nowUtc;
            if (meeting.Attendees.Count == 0)
                context.Meetings.Remove(meeting);
        }

        await context.SaveChangesOrConflictAsync(MeetingLimits.ChangedMeanwhile, ct);
        return meetings.Count;
    }
}
