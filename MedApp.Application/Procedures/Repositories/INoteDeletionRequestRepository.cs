using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Repositories;

/// <summary>
/// Requests from staff to delete notes on procedures. A request is made, and decided, together with a change to its
/// note (its <see cref="PatientProcedureNote.PendingDeletionRequestId"/>, or its deletion): the note is loaded for update
/// with <see cref="IPatientProcedureRepository"/> in the same request, so they share the DbContext, and one
/// <c>SaveChangesAsync</c> saves both.
/// </summary>
public interface INoteDeletionRequestRepository
{
    /// <summary>
    /// Adds a request, saving with it the change to the note it's about. If another request for the note was made
    /// meanwhile, nothing is saved and it's a 409.
    /// </summary>
    Task AddAsync(ProcedureNoteDeletionRequest request, CancellationToken ct);

    /// <summary>A request, read-only.</summary>
    Task<ProcedureNoteDeletionRequest?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>A request, tracked, so the next save saves changes to it.</summary>
    Task<ProcedureNoteDeletionRequest?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The requests about a procedure's notes still waiting for a decision, tracked.</summary>
    Task<IReadOnlyList<ProcedureNoteDeletionRequest>> GetPendingForProcedureForUpdateAsync(Guid procedureId, CancellationToken ct);

    /// <summary>
    /// Saves changes to requests loaded for update, with any change to their notes. If someone else decided the request,
    /// or changed the note, since it was loaded, nothing is saved and it's a 409.
    /// </summary>
    Task SaveAsync(CancellationToken ct);
}
