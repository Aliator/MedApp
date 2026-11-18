using MedApp.Domain.Letters;

namespace MedApp.Application.Letters.Repositories;

/// <summary>Letter templates.</summary>
public interface ILetterTemplateRepository
{
    /// <summary>Every template; archived ones only when asked for.</summary>
    Task<IReadOnlyList<LetterTemplate>> GetAllAsync(bool includeArchived, CancellationToken ct);

    Task<LetterTemplate?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Whether another template (not <paramref name="exceptId"/>) for the same specialty (null: for every specialty)
    /// has this name, ignoring case.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? specialtyId, Guid? exceptId, CancellationToken ct);

    /// <summary>Adds a template. Another with its name in its specialty, added meanwhile, is a 409.</summary>
    Task AddAsync(LetterTemplate template, CancellationToken ct);

    /// <summary>
    /// Saves a change to a template read as <paramref name="originalVersion"/>: if it has changed since, nothing is
    /// saved and it's a 409. Another with its name in its specialty, saved meanwhile, is a 409 too.
    /// </summary>
    Task UpdateAsync(LetterTemplate template, int originalVersion, CancellationToken ct);

    /// <summary>Which of these templates some letter was written from: they're archived, never deleted.</summary>
    Task<IReadOnlySet<Guid>> GetInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Whether some letter was written from the template.</summary>
    Task<bool> IsInUseAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Deletes a template no letter was written from. A letter written from it meanwhile (the foreign key refuses the
    /// delete) is a 409.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
