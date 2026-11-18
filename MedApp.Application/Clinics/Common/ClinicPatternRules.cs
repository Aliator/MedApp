using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Clinics;

namespace MedApp.Application.Clinics.Common;

/// <summary>
/// A clinic pattern's rules (centre plan decision 125): its fields' checks, shared by adding and changing one, and the
/// dates it makes sessions on. A pattern never makes two sessions on one date, and makes them only when asked.
/// </summary>
public static class ClinicPatternRules
{
    public static readonly string EveryWeeksMessage = $"Every must be 1 to {ClinicLimits.MaxEveryWeeks} weeks.";

    public const string UntilBeforeFrom = "The until date must be on or after the from date.";

    public static readonly string WeeksMessage = $"Sessions can be made for 1 to {ClinicLimits.MaxWeeksAhead} weeks ahead.";

    public const string AlreadyMade = "A session from this pattern is already on that date.";

    public const string AlreadyStarted = "That date's session would already have started.";

    /// <summary>The field rules for a pattern's command (its shape; rooms, specialties and people are checked in the handler).</summary>
    public static void Rules<T>(
        this AbstractValidator<T> validator,
        Func<T, Guid> staffUserId,
        Func<T, Guid> roomId,
        Func<T, Guid> specialtyId,
        Func<T, string?> dayOfWeek,
        Func<T, TimeOnly> starts,
        Func<T, TimeOnly> ends,
        Func<T, int> slotMinutes,
        Func<T, int> everyWeeks,
        Func<T, DateOnly> fromDate,
        Func<T, DateOnly?> untilDate)
    {
        validator.RuleFor(x => staffUserId(x)).NotEmpty().WithMessage("Clinician is required.").OverridePropertyName("StaffUserId");
        validator.RuleFor(x => roomId(x)).NotEmpty().WithMessage("Room is required.").OverridePropertyName("RoomId");
        validator.RuleFor(x => specialtyId(x)).NotEmpty().WithMessage("Specialty is required.").OverridePropertyName("SpecialtyId");
        validator.RuleFor(x => dayOfWeek(x))
            .Must(HoursValidation.IsDay)
            .WithMessage(HoursValidation.DayOfWeekMessage)
            .OverridePropertyName("DayOfWeek");
        validator.RuleFor(x => starts(x))
            .Must((x, start) => WorkingHours.WhyOutside(start, ends(x)) is null)
            .WithMessage(x => WorkingHours.WhyOutside(starts(x), ends(x)))
            .OverridePropertyName("Starts");
        validator.RuleFor(x => slotMinutes(x))
            .Must((x, slot) => WhyBadSlots(starts(x), ends(x), slot) is null)
            .WithMessage(x => WhyBadSlots(starts(x), ends(x), slotMinutes(x)))
            .OverridePropertyName("SlotMinutes");
        validator.RuleFor(x => everyWeeks(x))
            .InclusiveBetween(1, ClinicLimits.MaxEveryWeeks)
            .WithMessage(EveryWeeksMessage)
            .OverridePropertyName("EveryWeeks");
        validator.RuleFor(x => untilDate(x))
            .Must((x, until) => until is null || until >= fromDate(x))
            .WithMessage(UntilBeforeFrom)
            .OverridePropertyName("UntilDate");
    }

    private static string? WhyBadSlots(TimeOnly starts, TimeOnly ends, int slotMinutes)
    {
        // Any date will do: only the length counts.
        var date = new DateOnly(2026, 1, 5);
        return ClinicRules.WhyBadSlots(UkTime.ToUtc(date, starts), UkTime.ToUtc(date, ends), slotMinutes);
    }

    /// <summary>
    /// The dates a pattern makes sessions on from <paramref name="today"/> for <paramref name="weeks"/> weeks (today and
    /// the next weeks × 7 − 1 days): its day of the week, every <see cref="ClinicPattern.EveryWeeks"/> weeks counted from
    /// the first such day on or after its from date, up to its until date.
    /// </summary>
    public static IReadOnlyList<DateOnly> Dates(ClinicPattern pattern, DateOnly today, int weeks)
    {
        var anchor = pattern.FromDate;
        while (anchor.DayOfWeek != pattern.DayOfWeek)
            anchor = anchor.AddDays(1);

        var last = today.AddDays(weeks * 7 - 1);
        if (pattern.UntilDate is { } until && until < last)
            last = until;

        var step = 7 * Math.Max(1, pattern.EveryWeeks);
        var dates = new List<DateOnly>();
        for (var date = anchor; date <= last; date = date.AddDays(step))
        {
            if (date >= today)
                dates.Add(date);
        }

        return dates;
    }
}
