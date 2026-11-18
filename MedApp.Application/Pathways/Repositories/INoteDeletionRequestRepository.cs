using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Repositories;

/// <summary>
/// Requests from staff to delete notes on pathways. A request is made, and decided, together with a change to its
/// note (its <see cref="PatientPathwayNote.PendingDeletionRequestId"/>, or its deletion): the note is loaded for update
/// with <see cref="IPatientPathwayRepository"/> in the same request, so they share the DbContext, and one
/// <c>SaveChangesAsync</c> saves both.
/// </summary>
public interface INoteDeletionRequestRepository
{
    /// <summary>
    /// Adds a request, saving with it the change to the note it's about. If another request for the note was made
    /// meanwhile, nothing is saved and it's a 409.
    /// </summary>
    Task AddAsync(PathwayNoteDeletionRequest request, CancellationToken ct);

    /// <summary>A request, read-only.</summary>
    Task<PathwayNoteDeletionRequest?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>A request, tracked, so the next save saves changes to it.</summary>
    Task<PathwayNoteDeletionRequest?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The requests about a pathway's notes still waiting for a decision, tracked.</summary>
    Task<IReadOnlyList<PathwayNoteDeletionRequest>> GetPendingForPathwayForUpdateAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>
    /// Saves changes to requests loaded for update, with any change to their notes. If someone else decided the request,
    /// or changed the note, since it was loaded, nothing is saved and it's a 409.
    /// </summary>
    Task SaveAsync(CancellationToken ct);
}
