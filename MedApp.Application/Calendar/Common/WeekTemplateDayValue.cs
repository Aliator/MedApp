using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>The day template a week template uses on one weekday, as it's asked for (see <see cref="TemplateHistory"/>).</summary>
public sealed record WeekTemplateDayValue(DayOfWeek DayOfWeek, Guid DayTemplateId)
{
    public static WeekTemplateDayValue Of(WeekTemplateDay day) => new(day.DayOfWeek, day.DayTemplateId);

    /// <summary>The weekdays with a day template chosen, Monday to Friday.</summary>
    public static IReadOnlyList<WeekTemplateDayValue> From(Guid? monday, Guid? tuesday, Guid? wednesday, Guid? thursday, Guid? friday) =>
        new (DayOfWeek Day, Guid? Template)[]
            {
                (DayOfWeek.Monday, monday),
                (DayOfWeek.Tuesday, tuesday),
                (DayOfWeek.Wednesday, wednesday),
                (DayOfWeek.Thursday, thursday),
                (DayOfWeek.Friday, friday)
            }
            .Where(d => d.Template is not null)
            .Select(d => new WeekTemplateDayValue(d.Day, d.Template!.Value))
            .ToList();

    /// <summary>A new row of a week template for this value, valid from <paramref name="validFrom"/>.</summary>
    public WeekTemplateDay ToDay(Guid weekTemplateId, DateOnly validFrom) => new()
    {
        Id = Guid.NewGuid(),
        WeekTemplateId = weekTemplateId,
        DayOfWeek = DayOfWeek,
        DayTemplateId = DayTemplateId,
        ValidFrom = validFrom
    };
}
