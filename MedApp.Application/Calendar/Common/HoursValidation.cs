using FluentValidation;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Calendar.Common;

/// <summary>A day of someone's or a site's hours, as asked for: the day by name, and UK clock times.</summary>
public interface IHoursDay
{
    string DayOfWeek { get; }
    TimeOnly From { get; }
    TimeOnly To { get; }
}

/// <summary>
/// The rules for a set of hours, staff or site (centre plan decision 119): at most one entry per day of the week, each
/// a day by name ("Monday" to "Sunday"), between 06:00 and 23:00, on 5-minute boundaries, the end after the start.
/// </summary>
public static class HoursValidation
{
    public const string DayOfWeekMessage = "Day of week must be one of Monday to Sunday.";
    public const string DayRepeated = "Each day of the week can appear only once.";

    /// <summary>The day of the week an entry names (check <see cref="IsDay"/> first).</summary>
    public static DayOfWeek Day(string name) =>
        PathwayEnums.TryParse<DayOfWeek>(name, out var day) ? day : throw new ArgumentException(DayOfWeekMessage, nameof(name));

    public static bool IsDay(string? name) => PathwayEnums.TryParse<DayOfWeek>(name, out _);

    /// <summary>The rules for the list of days.</summary>
    public static void Days<T, TDay>(this AbstractValidator<T> validator, Func<T, IReadOnlyList<TDay>?> days)
        where TDay : IHoursDay
    {
        validator.RuleFor(x => days(x))
            .Must(list => list is not null)
            .WithMessage("Days are required (an empty list for the default).")
            .Must(list => list is null || list.Where(d => IsDay(d.DayOfWeek)).GroupBy(d => Day(d.DayOfWeek)).All(g => g.Count() == 1))
            .WithMessage(DayRepeated)
            .OverridePropertyName("Days");

        validator.RuleForEach(x => days(x) ?? Array.Empty<TDay>())
            .Must(d => IsDay(d.DayOfWeek))
            .WithMessage(DayOfWeekMessage)
            .Must(d => WorkingHours.WhyOutside(d.From, d.To) is null)
            .WithMessage((_, d) => WorkingHours.WhyOutside(d.From, d.To)!)
            .OverridePropertyName("Days");
    }
}
