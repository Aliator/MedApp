using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Mdt;
using MediatR;

namespace MedApp.Application.Mdt.Commands.DeferMdtItem;

public sealed class DeferMdtItemHandler(
    IMdtItemRepository repository,
    ITeamBoundary boundary,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<DeferMdtItemCommand, MdtItemResponse>
{
    public async Task<MdtItemResponse> Handle(DeferMdtItemCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetForUpdateAsync(request.ItemId, cancellationToken)
                   ?? throw new NotFoundException(MdtWalls.NotFoundMessage(request.ItemId));
        // Only the item's own team (its MDT's) moves or withdraws it: behind the wall a 404 (recorded), else a 409.
        await boundary.EnsureCanChangeAsync(item, request.Actor.Teams, cancellationToken);

        PatientRules.EnsureCanChange(item.Patient);
        MdtRules.EnsureVersion(item, request.ExpectedVersion);
        MdtRules.EnsureOpen(item);

        var meeting = await repository.GetMeetingAsync(request.MeetingId, cancellationToken)
                      ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        MdtRules.EnsureCanListFor(meeting, now, after: item.Meeting?.StartUtc);

        if (await repository.IsOpenOnMeetingAsync(item.PathwayId, meeting.Id, cancellationToken))
            throw new ConflictException(MdtRules.AlreadyListedMessage);

        var fromMeetingId = item.MeetingId;
        var fromStart = item.Meeting?.StartUtc ?? item.MeetingCancelledStartUtc;
        var team = await teams.GetAsync(meeting.TeamId!.Value, cancellationToken);

        // Deferred by the team when it was on a meeting; listed again, as it was, when its meeting was cancelled.
        if (fromMeetingId is not null)
            item.Status = MdtItemStatus.Deferred;
        // The meeting is tracked in the same context, so the key and the navigation agree.
        item.Meeting = meeting;
        item.MeetingId = meeting.Id;
        item.MeetingCancelledStartUtc = null;
        item.TeamId = meeting.TeamId.Value;
        // The presenter stays only if they're invited to the new MDT too (MC21).
        if (item.PresenterUserId is { } presenter && meeting.Attendees.All(a => a.StaffUserId != presenter))
        {
            item.PresenterUserId = null;
            item.PresenterUsername = null;
        }

        item.Version++;

        // The grants follow it: the new MDT's attendees read it, the old one's stop (MC26).
        await repository.SyncGrantsAsync([item], now, cancellationToken);
        await repository.SaveAsync(
            MdtAudit.Deferred(item, team?.Name ?? string.Empty, fromMeetingId, fromStart, meeting, request.Actor, now),
            cancellationToken);

        var saved = await repository.GetAsync(item.Id, cancellationToken)
                    ?? throw new NotFoundException($"MDT item '{item.Id}' was not found.");
        return await repository.ToResponseAsync(saved, request.Actor, now, cancellationToken);
    }
}
