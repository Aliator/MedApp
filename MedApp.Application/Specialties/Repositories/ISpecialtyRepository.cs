using MedApp.Application.Specialties.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Specialties;

namespace MedApp.Application.Specialties.Repositories;

/// <summary>Specialties, and which members of staff belong to which.</summary>
public interface ISpecialtyRepository
{
    /// <summary>Every specialty, sorted by name; archived ones only when asked for.</summary>
    Task<IReadOnlyList<Specialty>> GetAllAsync(bool includeArchived, CancellationToken ct);

    Task<Specialty?> GetAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Specialty>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Whether another specialty (not <paramref name="exceptId"/>) already has this name, ignoring case.</summary>
    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How many templates, pathways and members of staff have each of these specialties (only those any have).</summary>
    Task<IReadOnlyDictionary<Guid, SpecialtyUsage>> GetUsageAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Adds a specialty. Another with the same name, added meanwhile, is a 409.</summary>
    Task AddAsync(Specialty specialty, CancellationToken ct);

    /// <summary>
    /// Saves a change to a specialty read as <paramref name="originalVersion"/>: if it has changed since (another
    /// admin saved first), nothing is saved and it's a 409. Another with the same name, saved meanwhile, is a 409 too.
    /// </summary>
    Task UpdateAsync(Specialty specialty, int originalVersion, CancellationToken ct);

    /// <summary>Deletes a specialty. One that came into use meanwhile (the database refuses) is a 409.</summary>
    Task DeleteAsync(Specialty specialty, CancellationToken ct);

    /// <summary>The specialties a member of staff belongs to, sorted by name.</summary>
    Task<IReadOnlyList<Specialty>> GetForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Sets the specialties a member of staff belongs to, exactly, and saves <paramref name="activity"/> in the same
    /// save, so the change can't miss the activity log.
    /// </summary>
    Task SetForUserAsync(Guid userId, IReadOnlyCollection<Guid> specialtyIds, ActivityEntry activity, CancellationToken ct);
}
