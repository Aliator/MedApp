using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

public static class CalendarRepositoryExtensions
{
    /// <summary>The activity type, or a 404.</summary>
    public static async Task<CalendarActivityType> GetActivityTypeOrThrowAsync(
        this ICalendarSetupRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetActivityTypeAsync(id, ct)
        ?? throw new NotFoundException($"Activity type '{id}' was not found.");

    /// <summary>
    /// An activity type a block can be given: it exists (404), and isn't archived (409) unless the block already has
    /// it (<paramref name="currentActivityTypeId"/>).
    /// </summary>
    public static async Task<CalendarActivityType> GetActivityTypeForBlockAsync(
        this ICalendarSetupRepository repository, Guid id, Guid? currentActivityTypeId, CancellationToken ct)
    {
        var activityType = await repository.GetActivityTypeOrThrowAsync(id, ct);
        if (activityType.IsArchived && activityType.Id != currentActivityTypeId)
            throw new ConflictException($"The '{activityType.Name}' activity type is archived, so it can't be used for new blocks.");

        return activityType;
    }

    /// <summary>The day template with its blocks valid today, read-only, or a 404.</summary>
    public static async Task<DayTemplate> GetDayTemplateOrThrowAsync(
        this ICalendarSetupRepository repository, Guid id, DateOnly today, CancellationToken ct) =>
        await repository.GetDayTemplateAsync(id, today, ct)
        ?? throw new NotFoundException($"Day template '{id}' was not found.");

    /// <summary>The day template with its blocks valid today, tracked for changes, or a 404.</summary>
    public static async Task<DayTemplate> GetDayTemplateForUpdateOrThrowAsync(
        this ICalendarSetupRepository repository, Guid id, DateOnly today, CancellationToken ct) =>
        await repository.GetDayTemplateForUpdateAsync(id, today, ct)
        ?? throw new NotFoundException($"Day template '{id}' was not found.");

    /// <summary>
    /// One of this person's own day templates (a day override can only use theirs), read-only, or a 404, which is also
    /// what someone else's gets.
    /// </summary>
    public static async Task<DayTemplate> GetOwnDayTemplateOrThrowAsync(
        this ICalendarSetupRepository repository, Guid id, Guid ownerUserId, DateOnly today, CancellationToken ct)
    {
        var template = await repository.GetDayTemplateOrThrowAsync(id, today, ct);
        return template.OwnerUserId == ownerUserId
            ? template
            : throw new NotFoundException($"Day template '{id}' isn't one of this person's templates.");
    }

    /// <summary>
    /// One of this person's own week templates (their default week can only be theirs), read-only, or a 404, which is
    /// also what someone else's gets.
    /// </summary>
    public static async Task<WeekTemplate> GetOwnWeekTemplateOrThrowAsync(
        this ICalendarSetupRepository repository, Guid id, Guid ownerUserId, DateOnly today, CancellationToken ct)
    {
        var template = await repository.GetWeekTemplateOrThrowAsync(id, today, ct);
        return template.OwnerUserId == ownerUserId
            ? template
            : throw new NotFoundException($"Week template '{id}' isn't one of this person's templates.");
    }

    /// <summary>The week template with its days valid today, read-only, or a 404.</summary>
    public static async Task<WeekTemplate> GetWeekTemplateOrThrowAsync(
        this ICalendarSetupRepository repository, Guid id, DateOnly today, CancellationToken ct) =>
        await repository.GetWeekTemplateAsync(id, today, ct)
        ?? throw new NotFoundException($"Week template '{id}' was not found.");

    /// <summary>The week template with its days valid today, tracked for changes, or a 404.</summary>
    public static async Task<WeekTemplate> GetWeekTemplateForUpdateOrThrowAsync(
        this ICalendarSetupRepository repository, Guid id, DateOnly today, CancellationToken ct) =>
        await repository.GetWeekTemplateForUpdateAsync(id, today, ct)
        ?? throw new NotFoundException($"Week template '{id}' was not found.");
}
