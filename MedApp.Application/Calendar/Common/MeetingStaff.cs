using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>Who a meeting is for, and whether they can all go at its time.</summary>
public static class MeetingStaff
{
    public const string NobodyLeft =
        "Nobody left on this meeting can go: their accounts have gone or they're no longer staff. Add someone, or cancel it.";

    /// <summary>
    /// The members of staff with these IDs (each once), by username, or a 404 naming any that aren't staff (no
    /// account, or no role).
    /// </summary>
    public static async Task<IReadOnlyList<(Guid UserId, string Username)>> GetStaffOrThrowAsync(
        this IIdentityReadService identity, IEnumerable<Guid> staffUserIds, CancellationToken ct) =>
        ChooseOrThrow(await identity.GetStaffAsync(ct), staffUserIds, alreadyGoing: []);

    /// <summary>
    /// The attendees a meeting asks for, from <paramref name="staff"/> (everyone with a staff role). Only those not
    /// <paramref name="alreadyGoing"/> must be staff: a 404 names any that aren't. Anyone already going whose account
    /// has since gone, or who is no longer staff, is left out without an error, so a change drops them (they can't go,
    /// and an account that goes is taken off its meetings to come anyway). A 400 when that leaves nobody.
    /// </summary>
    public static IReadOnlyList<(Guid UserId, string Username)> ChooseOrThrow(
        IReadOnlyList<(Guid UserId, string Username)> staff,
        IEnumerable<Guid> staffUserIds,
        IReadOnlyCollection<Guid> alreadyGoing)
    {
        var wanted = staffUserIds.Distinct().ToList();
        var chosen = staff.Where(s => wanted.Contains(s.UserId)).ToList();

        var missing = wanted.Where(id => !alreadyGoing.Contains(id) && chosen.All(s => s.UserId != id)).ToList();
        if (missing.Count != 0)
            throw new NotFoundException($"Staff member(s) not found: {string.Join(", ", missing)}.");

        // Something was asked for, but everyone asked for has gone.
        if (wanted.Count != 0 && chosen.Count == 0)
            throw new ValidationException([new ValidationFailure(nameof(IMeetingCommand.AttendeeUserIds), NobodyLeft)]);

        return chosen;
    }

    /// <summary>
    /// A 409 naming the first attendee, by username, who has something on at that time ("k.ashford has Lunch, 12:00 to
    /// 13:00."): an appointment, a block, or another meeting (<paramref name="ignoreMeetingId"/>: the one being moved).
    /// Run it holding the attendees' calendar locks.
    /// </summary>
    public static async Task EnsureEveryoneFreeAsync(
        this ICalendarRepository repository,
        IReadOnlyList<(Guid UserId, string Username)> attendees,
        DateTime startUtc,
        DateTime endUtc,
        Guid? ignoreMeetingId,
        CancellationToken ct)
    {
        var date = UkTime.DateOf(startUtc);
        var calendars = (await repository.GetCalendarsAsync(attendees.Select(a => a.UserId).ToList(), date, date, ct))
            .ToDictionary(d => d.StaffUserId);

        foreach (var (userId, username) in attendees)
        {
            if (CalendarClashes.FindFirst(calendars[userId], startUtc, endUtc, ignoreMeetingId: ignoreMeetingId) is { } clash)
                throw new ConflictException(
                    $"{username} has {clash.What}, {UkTime.DescribeTime(clash.StartUtc)} to {UkTime.DescribeTime(clash.EndUtc)}.");
        }
    }

    /// <summary>The attendees as stored: each member of staff once.</summary>
    public static List<MeetingAttendee> Attendees(Guid meetingId, IEnumerable<Guid> staffUserIds) =>
        staffUserIds.Distinct().Select(id => new MeetingAttendee { MeetingId = meetingId, StaffUserId = id }).ToList();
}
