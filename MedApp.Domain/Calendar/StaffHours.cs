namespace MedApp.Domain.Calendar;

/// <summary>
/// The hours a member of staff works on one day of the week, and at which site, over a range of dates (see
/// <see cref="IDatedRow"/>): changing them applies from that day, and past days keep what applied then. A person with no
/// rows valid on a date works the default (Monday to Friday, 08:00 to 18:00, at no particular site); one with rows
/// works only the days those rows name. Set by holders of ManageClinics or ManageAnyCalendar (centre plan decision 119).
/// </summary>
public sealed class StaffHours : IDatedRow
{
    public Guid Id { get; init; }

    /// <summary>No foreign key to the users table, as with default week templates.</summary>
    public Guid StaffUserId { get; init; }

    /// <summary>Any day of the week (stored as .NET's number: Sunday 0 to Saturday 6).</summary>
    public DayOfWeek DayOfWeek { get; init; }

    /// <summary>UK clock time, between 06:00 and 23:00, on a 5-minute boundary.</summary>
    public TimeOnly Starts { get; init; }

    /// <summary>UK clock time, after <see cref="Starts"/>, at most 23:00.</summary>
    public TimeOnly Ends { get; init; }

    /// <summary>Where they work that day: its hours and its closed days apply.</summary>
    public Guid SiteId { get; init; }

    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; set; }
}
