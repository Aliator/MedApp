using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>Checks what a template is being given, as <c>TemplateStages</c> does for procedure templates.</summary>
public static class TemplateChecks
{
    /// <summary>
    /// Every activity type a day template's blocks name must exist (404), and archived ones may only stay where the
    /// template already has them (409).
    /// </summary>
    public static async Task CheckActivityTypesAsync(
        ICalendarSetupRepository repository,
        IEnumerable<DayTemplateBlockValue> blocks,
        IReadOnlySet<Guid> alreadyInTemplate,
        CancellationToken ct)
    {
        var ids = blocks.Select(b => b.ActivityTypeId).Distinct().ToList();
        var activityTypes = (await repository.GetActivityTypesByIdAsync(ids, ct)).ToDictionary(t => t.Id);

        var missing = ids.Where(id => !activityTypes.ContainsKey(id)).ToList();
        if (missing.Count != 0)
            throw new NotFoundException($"Activity type(s) not found: {string.Join(", ", missing)}.");

        var archived = ids
            .Where(id => activityTypes[id].IsArchived && !alreadyInTemplate.Contains(id))
            .Select(id => activityTypes[id].Name)
            .ToList();
        if (archived.Count != 0)
            throw new ConflictException($"Archived activity types can't be added to a day template: {string.Join(", ", archived)}.");
    }

    /// <summary>
    /// Every day template a week template's days name must exist and be its owner's own (404: someone else's isn't one
    /// of theirs), and archived ones may only stay where the template already has them (409).
    /// </summary>
    public static async Task CheckDayTemplatesAsync(
        ICalendarSetupRepository repository,
        Guid ownerUserId,
        IEnumerable<WeekTemplateDayValue> days,
        IReadOnlySet<Guid> alreadyInTemplate,
        CancellationToken ct)
    {
        var ids = days.Select(d => d.DayTemplateId).Distinct().ToList();
        var dayTemplates = (await repository.GetDayTemplatesByIdAsync(ids, ct))
            .Where(t => t.OwnerUserId == ownerUserId)
            .ToDictionary(t => t.Id);

        var missing = ids.Where(id => !dayTemplates.ContainsKey(id)).ToList();
        if (missing.Count != 0)
            throw new NotFoundException($"Day template(s) not found among this person's templates: {string.Join(", ", missing)}.");

        var archived = ids
            .Where(id => dayTemplates[id].IsArchived && !alreadyInTemplate.Contains(id))
            .Select(id => dayTemplates[id].Name)
            .ToList();
        if (archived.Count != 0)
            throw new ConflictException($"Archived day templates can't be added to a week template: {string.Join(", ", archived)}.");
    }

    /// <summary>The activity types a day template's current blocks use.</summary>
    public static IReadOnlySet<Guid> ActivityTypesIn(DayTemplate template) =>
        template.Blocks.Select(b => b.ActivityTypeId).ToHashSet();

    /// <summary>The day templates a week template's current days use.</summary>
    public static IReadOnlySet<Guid> DayTemplatesIn(WeekTemplate template) =>
        template.Days.Select(d => d.DayTemplateId).ToHashSet();
}
