using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Requests to delete notes. A request's note is loaded for update by <c>PatientPathwayRepository</c> in the same
/// request, so it shares this DbContext, and each save here saves the change to the note too (and a pathway change's
/// save there saves the request).
/// </summary>
public sealed class NoteDeletionRequestRepository(MedAppDbContext context) : INoteDeletionRequestRepository
{
    public async Task AddAsync(PathwayNoteDeletionRequest request, CancellationToken ct)
    {
        context.PathwayNoteDeletionRequests.Add(request);
        try
        {
            // The note's row version: someone else asked, or an admin deleted or its author edited it, since it was loaded.
            await context.SaveChangesOrConflictAsync(PathwayNotes.RequestedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(PathwayNoteDeletionRequestConfiguration.PendingPerNoteIndex))
        {
            throw new ConflictException(PathwayNotes.AlreadyRequestedMessage(null));
        }
    }

    public async Task<PathwayNoteDeletionRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.PathwayNoteDeletionRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<PathwayNoteDeletionRequest?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.PathwayNoteDeletionRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<PathwayNoteDeletionRequest>> GetPendingForPathwayForUpdateAsync(Guid pathwayId, CancellationToken ct) =>
        await context.PathwayNoteDeletionRequests
            .Where(r => r.PatientPathwayId == pathwayId && r.Status == NoteDeletionRequestStatus.Pending)
            .ToListAsync(ct);

    public Task SaveAsync(CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(PathwayNotes.DeletedMeanwhileMessage, ct);
}
