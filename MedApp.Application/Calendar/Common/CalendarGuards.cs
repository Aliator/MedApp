using MedApp.Application.Common.Exceptions;

namespace MedApp.Application.Calendar.Common;

/// <summary>Who may change a calendar, and when: its owner or an admin, and never on a past day.</summary>
public static class CalendarGuards
{
    public const string PastDay = "Past days can't be changed.";

    /// <summary>A 403 unless the actor may change this person's calendar (<see cref="CalendarActor.CanChangeCalendarOf"/>).</summary>
    public static void EnsureCanChange(this CalendarActor actor, Guid staffUserId)
    {
        if (!actor.CanChangeCalendarOf(staffUserId))
            throw new ForbiddenException(CalendarActor.NotYourCalendar);
    }

    /// <summary>A 403 unless the actor may change this person's day and week templates (their own, or anyone's for an admin).</summary>
    public static void EnsureCanChangeTemplatesOf(this CalendarActor actor, Guid ownerUserId)
    {
        if (!actor.CanChangeCalendarOf(ownerUserId))
            throw new ForbiddenException(CalendarActor.NotYourTemplates);
    }

    /// <summary>A 409 for a UK date before today: past days keep what they had.</summary>
    public static void EnsureNotPast(DateOnly date, DateOnly today)
    {
        if (date < today)
            throw new ConflictException(PastDay);
    }
}
