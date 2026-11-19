using MedApp.Domain.Lab;

namespace MedApp.Application.Lab.Repositories;

/// <summary>The lab's test catalogue (MC13).</summary>
public interface ILabTestRepository
{
    /// <summary>Every test by name, with its steps in order; archived ones only when asked.</summary>
    Task<IReadOnlyList<LabTest>> GetAllAsync(bool includeArchived, CancellationToken ct);

    /// <summary>One test with its steps, tracked for changes, or null.</summary>
    Task<LabTest?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Whether another test has this name, ignoring case.</summary>
    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>Which of these tests a lab case or sample has used (so they're archived, not deleted).</summary>
    Task<IReadOnlySet<Guid>> GetInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task AddAsync(LabTest test, CancellationToken ct);

    /// <summary>Saves a tracked test's changes, adding <paramref name="newSteps"/> and removing <paramref name="removedSteps"/>.</summary>
    Task UpdateAsync(LabTest test, IEnumerable<LabTestStep> newSteps, IEnumerable<LabTestStep> removedSteps, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}
