using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Repositories;

/// <summary>The days the clinic is closed (bank holidays and others), one per UK date.</summary>
public interface IClosedDayRepository
{
    /// <summary>The closed days from <paramref name="from"/> to <paramref name="to"/> (either may be open), by date, read-only.</summary>
    Task<IReadOnlyList<ClosedDay>> GetAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    Task<ClosedDay?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The closed day on a date, if any, read-only.</summary>
    Task<ClosedDay?> GetOnAsync(DateOnly date, CancellationToken ct);

    /// <summary>
    /// Adds closed days in one save. Throws <see cref="Common.Exceptions.ConflictException"/> if one's date is already
    /// closed (the database's unique index: another request added it after this one's check).
    /// </summary>
    Task AddAsync(IReadOnlyCollection<ClosedDay> days, CancellationToken ct);

    Task DeleteAsync(ClosedDay day, CancellationToken ct);
}
