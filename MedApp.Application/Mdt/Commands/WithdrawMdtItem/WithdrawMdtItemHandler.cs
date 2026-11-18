using MedApp.Application.Common.Exceptions;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Mdt;
using MediatR;

namespace MedApp.Application.Mdt.Commands.WithdrawMdtItem;

public sealed class WithdrawMdtItemHandler(IMdtItemRepository repository, TimeProvider timeProvider)
    : IRequestHandler<WithdrawMdtItemCommand, MdtItemResponse>
{
    public async Task<MdtItemResponse> Handle(WithdrawMdtItemCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetForUpdateAsync(request.ItemId, cancellationToken)
                   ?? throw new NotFoundException($"MDT item '{request.ItemId}' was not found.");

        PatientRules.EnsureCanPause(item.Patient);
        MdtRules.EnsureVersion(item, request.ExpectedVersion);
        MdtRules.EnsureOpen(item);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        item.Status = MdtItemStatus.Withdrawn;
        item.WithdrawnByUserId = request.Actor.UserId;
        item.WithdrawnByUsername = request.Actor.Username;
        item.WithdrawnAtUtc = now;
        item.Version++;

        await repository.SaveAsync(MdtAudit.Withdrawn(item, item.Specialty?.Name ?? string.Empty, request.Actor, now), cancellationToken);
        return MdtMapper.ToResponse(item, request.Actor, now);
    }
}
