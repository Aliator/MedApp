using MedApp.Application.Practices.Common;
using MedApp.Domain.Practices;

namespace MedApp.Application.Practices.Repositories;

/// <summary>The GP practice directory.</summary>
public interface IPracticeRepository
{
    /// <summary>Every practice, sorted by name; archived ones only when asked for.</summary>
    Task<IReadOnlyList<Practice>> GetAllAsync(bool includeArchived, CancellationToken ct);

    Task<Practice?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>The practices with these IDs; IDs with none are left out.</summary>
    Task<IReadOnlyList<Practice>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Whether another practice (not <paramref name="exceptId"/>) has this ODS code (upper case).</summary>
    Task<bool> OdsCodeExistsAsync(string odsCode, Guid? exceptId, CancellationToken ct);

    /// <summary>How many patients and referrals have each of these practices (only those any have).</summary>
    Task<IReadOnlyDictionary<Guid, PracticeUsage>> GetUsageAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Adds a practice. Another with the same ODS code, added meanwhile, is a 409.</summary>
    Task AddAsync(Practice practice, CancellationToken ct);

    /// <summary>
    /// Saves a change to a practice read as <paramref name="originalVersion"/>: if it has changed since, nothing is saved
    /// and it's a 409. Another with the same ODS code, saved meanwhile, is a 409 too.
    /// </summary>
    Task UpdateAsync(Practice practice, int originalVersion, CancellationToken ct);

    /// <summary>Deletes a practice. One that came into use meanwhile (the database refuses) is a 409.</summary>
    Task DeleteAsync(Practice practice, CancellationToken ct);
}
