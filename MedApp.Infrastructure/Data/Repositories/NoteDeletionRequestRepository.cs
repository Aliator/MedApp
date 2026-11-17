using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Requests to delete notes. A request's note is loaded for update by <c>PatientProcedureRepository</c> in the same
/// request, so it shares this DbContext, and each save here saves the change to the note too (and a procedure change's
/// save there saves the request).
/// </summary>
public sealed class NoteDeletionRequestRepository(MedAppDbContext context) : INoteDeletionRequestRepository
{
    public async Task AddAsync(ProcedureNoteDeletionRequest request, CancellationToken ct)
    {
        context.ProcedureNoteDeletionRequests.Add(request);
        try
        {
            // The note's row version: someone else asked, or an admin deleted or its author edited it, since it was loaded.
            await context.SaveChangesOrConflictAsync(ProcedureNotes.RequestedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(ProcedureNoteDeletionRequestConfiguration.PendingPerNoteIndex))
        {
            throw new ConflictException(ProcedureNotes.AlreadyRequestedMessage(null));
        }
    }

    public async Task<ProcedureNoteDeletionRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.ProcedureNoteDeletionRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<ProcedureNoteDeletionRequest?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.ProcedureNoteDeletionRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<ProcedureNoteDeletionRequest>> GetPendingForProcedureForUpdateAsync(Guid procedureId, CancellationToken ct) =>
        await context.ProcedureNoteDeletionRequests
            .Where(r => r.PatientProcedureId == procedureId && r.Status == NoteDeletionRequestStatus.Pending)
            .ToListAsync(ct);

    public Task SaveAsync(CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(ProcedureNotes.DeletedMeanwhileMessage, ct);
}
