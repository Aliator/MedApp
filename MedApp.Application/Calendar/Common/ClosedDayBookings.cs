using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Calendar.Common;

/// <summary>What one member of staff has on a day: appointments (Booked, Attended, Did not attend), stored blocks and meetings.</summary>
public sealed record StaffBookings(Guid StaffUserId, int Appointments, int Blocks, int Meetings);

/// <summary>
/// What's already booked on a day that's being closed (decision 75): it stays, so the admin is told what's there, and
/// each member of staff with something on it is notified (<see cref="NotificationKind.DayClosed"/>), never the admin who
/// closed it. Counts and usernames only, never a patient.
/// </summary>
public static class ClosedDayBookings
{
    public const string NotificationTitle = "Day closed";

    /// <summary>The day's bookings as the API returns them, staff by username.</summary>
    public static async Task<DayBookingsResponse> ToResponseAsync(
        DateOnly date, IReadOnlyList<StaffBookings> bookings, IIdentityReadService identity, CancellationToken ct)
    {
        var usernames = bookings.Count == 0
            ? new Dictionary<Guid, string>()
            : await identity.GetUsernamesByIdAsync(bookings.Select(b => b.StaffUserId), ct);

        return new DayBookingsResponse
        {
            Date = date,
            Appointments = bookings.Sum(b => b.Appointments),
            Blocks = bookings.Sum(b => b.Blocks),
            Meetings = bookings.Sum(b => b.Meetings),
            Staff = bookings
                .Select(b => new DayBookingsStaffResponse
                {
                    UserId = b.StaffUserId,
                    Username = usernames.GetValueOrDefault(b.StaffUserId) ?? ProcedureMapper.DeletedUser,
                    Appointments = b.Appointments,
                    Blocks = b.Blocks,
                    Meetings = b.Meetings
                })
                .OrderBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.UserId)
                .ToList()
        };
    }

    /// <summary>
    /// A notification for each member of staff with something on the day closed, but the admin who closed it: a fixed
    /// title, and a message of who closed which day, the closed day's admin-written name, and what they have then.
    /// </summary>
    public static IReadOnlyList<Notification> Notifications(
        DateOnly date, string closedDayName, IReadOnlyList<StaffBookings> bookings, CalendarActor actor, DateTime nowUtc) =>
        bookings
            .Where(b => b.StaffUserId != actor.UserId)
            .Select(b => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = b.StaffUserId,
                Kind = NotificationKind.DayClosed,
                Title = NotificationTitle,
                // What's counted includes what has already happened (a day closed part-way through) and blocks, so
                // the message asks only for what's still to come to be moved or cancelled.
                Message = $"{actor.Username} closed {UkTime.DescribeDate(date)} ({closedDayName}). " +
                          $"You have {What(b)} then. Move or cancel what's still to come, and tell any patients.",
                CalendarDate = date,
                CreatedAtUtc = nowUtc
            })
            .ToList();

    /// <summary>"2 appointments, a block and a meeting".</summary>
    public static string What(StaffBookings b)
    {
        var parts = new[] { Part(b.Appointments, "appointment"), Part(b.Blocks, "block"), Part(b.Meetings, "meeting") }
            .OfType<string>()
            .ToList();
        return parts.Count switch
        {
            0 => "nothing",
            1 => parts[0],
            _ => $"{string.Join(", ", parts[..^1])} and {parts[^1]}"
        };
    }

    private static string? Part(int count, string noun) => count switch
    {
        0 => null,
        1 => $"a{(noun[0] is 'a' ? "n" : "")} {noun}",
        _ => $"{count} {noun}s"
    };
}
