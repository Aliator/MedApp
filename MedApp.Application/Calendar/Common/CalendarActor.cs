using MedApp.Application.Common.Identity;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The signed-in staff member, for calendar changes, and what their roles allow (<paramref name="Permissions"/>): anyone
/// can read any calendar, but only its owner or a holder of <see cref="Permission.ManageAnyCalendar"/> changes it, and
/// only holders of <see cref="Permission.ArrangeMeetings"/> change meetings.
/// </summary>
public sealed record CalendarActor(Guid UserId, string Username, IReadOnlySet<Permission>? Permissions = null)
{
    public const string NotYourCalendar = "You can't change someone else's calendar.";
    public const string NotYourTemplates = "You can't change someone else's calendar templates.";
    public const string CantSetHours = "Only staff who manage clinics or anyone's calendar can set working hours.";

    /// <summary>Whether their roles grant this permission.</summary>
    public bool Has(Permission permission) => Permissions?.Contains(permission) == true;

    /// <summary>
    /// Whether they may set anyone's working hours (<see cref="Permission.ManageClinics"/> or
    /// <see cref="Permission.ManageAnyCalendar"/>, centre plan decision 119).
    /// </summary>
    public bool CanSetHours => Has(Permission.ManageClinics) || Has(Permission.ManageAnyCalendar);

    /// <summary>Whether they may add, change and cancel clinic sessions and patterns (centre plan decision 125).</summary>
    public bool CanManageClinics => Has(Permission.ManageClinics);

    /// <summary>Whether they may arrange, change and cancel meetings.</summary>
    public bool CanArrangeMeetings => Has(Permission.ArrangeMeetings);

    /// <summary>
    /// Their own calendar, or anyone's with <see cref="Permission.ManageAnyCalendar"/>: its blocks, a day's template,
    /// the default week, and the day and week templates they own.
    /// </summary>
    public bool CanChangeCalendarOf(Guid staffUserId) => Has(Permission.ManageAnyCalendar) || staffUserId == UserId;

    /// <summary>Records compare their permissions by content, so two actors with the same set are equal.</summary>
    public bool Equals(CalendarActor? other) =>
        other is not null
        && UserId == other.UserId
        && Username == other.Username
        && (Permissions ?? Empty).SetEquals(other.Permissions ?? Empty);

    public override int GetHashCode() => HashCode.Combine(UserId, Username, Permissions?.Count ?? 0);

    private static readonly HashSet<Permission> Empty = [];
}
