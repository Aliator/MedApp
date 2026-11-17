namespace MedApp.Application.Calendar.Common;

/// <summary>The signed-in staff member, for calendar changes: anyone can read any calendar, but only its owner or an admin changes it.</summary>
public sealed record CalendarActor(Guid UserId, string Username, bool IsAdmin)
{
    public const string NotYourCalendar = "Only an admin can change someone else's calendar.";
    public const string NotYourTemplates = "Only an admin can change someone else's calendar templates.";

    /// <summary>
    /// Their own calendar, or anyone's for an admin: its blocks, a day's template, the default week, and the day and
    /// week templates they own.
    /// </summary>
    public bool CanChangeCalendarOf(Guid staffUserId) => IsAdmin || staffUserId == UserId;
}
