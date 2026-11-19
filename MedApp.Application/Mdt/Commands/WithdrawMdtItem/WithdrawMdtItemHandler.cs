using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Mdt;
using MediatR;

namespace MedApp.Application.Mdt.Commands.WithdrawMdtItem;

public sealed class WithdrawMdtItemHandler(
    IMdtItemRepository repository, ITeamBoundary boundary, TimeProvider timeProvider)
    : IRequestHandler<WithdrawMdtItemCommand, MdtItemResponse>
{
    public async Task<MdtItemResponse> Handle(WithdrawMdtItemCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetForUpdateAsync(request.ItemId, cancellationToken)
                   ?? throw new NotFoundException(MdtWalls.NotFoundMessage(request.ItemId));
        // Only the item's own team (its MDT's) moves or withdraws it: behind the wall a 404 (recorded), else a 409.
        await boundary.EnsureCanChangeAsync(item, request.Actor.Teams, cancellationToken);

        PatientRules.EnsureCanPause(item.Patient);
        MdtRules.EnsureVersion(item, request.ExpectedVersion);
        MdtRules.EnsureOpen(item);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        item.Status = MdtItemStatus.Withdrawn;
        item.WithdrawnByUserId = request.Actor.UserId;
        item.WithdrawnByUsername = request.Actor.Username;
        item.WithdrawnAtUtc = now;
        item.Version++;

        await repository.SaveAsync(MdtAudit.Withdrawn(item, item.Team?.Name ?? string.Empty, request.Actor, now), cancellationToken);
        return MdtMapper.ToResponse(item, request.Actor, now);
    }
}
