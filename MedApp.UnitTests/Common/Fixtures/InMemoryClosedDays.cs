using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Domain.Calendar;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Closed days kept in memory, for validators whose working day rule looks them up (<see cref="WorkingDays"/>): none
/// unless given. Its tasks are already complete, so <see cref="ValidatorExtensions.ValidateNow{T}"/> can run those
/// validators in a test without awaiting.
/// </summary>
public sealed class InMemoryClosedDays(params ClosedDay[] days) : IClosedDayRepository
{
    private readonly List<ClosedDay> _days = [.. days];

    public IReadOnlyList<ClosedDay> Days => _days;

    /// <summary>The working day's rules with these closed days, everyone on the default hours.</summary>
    public static WorkingDays WorkingDays(params ClosedDay[] days) => new(new InMemoryClosedDays(days), new InMemoryWorkingHours());

    /// <summary>The working day's rules with these hours and closed days.</summary>
    public static WorkingDays WorkingDays(InMemoryWorkingHours hours, params ClosedDay[] days) =>
        new(new InMemoryClosedDays(days), hours);

    /// <summary>A closed day on a date: everywhere, or at a site.</summary>
    public static ClosedDay On(DateOnly date, string name = "Christmas Day", Guid? siteId = null) =>
        new() { Id = Guid.NewGuid(), Date = date, Name = name, SiteId = siteId };

    public Task<IReadOnlyList<ClosedDay>> GetAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ClosedDay>>(_days
            .Where(d => (from is null || d.Date >= from) && (to is null || d.Date <= to))
            .OrderBy(d => d.Date)
            .ToList());

    public Task<ClosedDay?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_days.FirstOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<ClosedDay>> GetOnAsync(DateOnly date, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ClosedDay>>(_days.Where(d => d.Date == date).ToList());

    public Task AddAsync(IReadOnlyCollection<ClosedDay> days, CancellationToken ct)
    {
        _days.AddRange(days);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ClosedDay day, CancellationToken ct)
    {
        _days.Remove(day);
        return Task.CompletedTask;
    }
}

public static class ValidatorExtensions
{
    /// <summary>
    /// Validates with rules that may be asynchronous (FluentValidation refuses those in <c>Validate</c>), for tests
    /// whose lookups are in memory (<see cref="InMemoryClosedDays"/>), so it finishes at once.
    /// </summary>
    public static ValidationResult ValidateNow<T>(this IValidator<T> validator, T instance) =>
        validator.ValidateAsync(instance).GetAwaiter().GetResult();
}
