using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>Changing or removing a day template's block on one day of one calendar, which leaves the template as it is.</summary>
public static class TemplateBlockForDay
{
    /// <summary>
    /// The template block as it falls on that date of the calendar <paramref name="data"/> covers, or a 404 if it
    /// isn't there (another day template applies, it's not valid that day, or it's already been changed or removed).
    /// </summary>
    public static ProjectedBlock Find(CalendarData data, Guid templateBlockId, DateOnly date) =>
        CalendarProjection.TemplateBlocks(data).FirstOrDefault(p => p.Source.Id == templateBlockId && p.Date == date)
        ?? throw new NotFoundException($"That template block isn't on this calendar on {UkTime.DescribeDate(date)}.");

    /// <summary>The exception that leaves the template block off that date of the person's calendar.</summary>
    public static CalendarBlockException Exception(Guid staffUserId, DateOnly date, ProjectedBlock block) => new()
    {
        Id = Guid.NewGuid(),
        StaffUserId = staffUserId,
        Date = date,
        DayTemplateBlockId = block.Source.Id
    };
}
