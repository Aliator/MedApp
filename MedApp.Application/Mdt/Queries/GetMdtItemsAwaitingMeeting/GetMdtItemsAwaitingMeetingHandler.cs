using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetMdtItemsAwaitingMeeting;

public sealed class GetMdtItemsAwaitingMeetingHandler(
    IMdtItemRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetMdtItemsAwaitingMeetingQuery, IReadOnlyList<MdtItemResponse>>
{
    public async Task<IReadOnlyList<MdtItemResponse>> Handle(GetMdtItemsAwaitingMeetingQuery request, CancellationToken cancellationToken)
    {
        var scope = request.Viewer.Teams;
        var items = await repository.GetAwaitingMeetingAsync(request.TeamId, scope, cancellationToken);
        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, items.Select(i => i.PatientId).Distinct(), cancellationToken,
            details: TeamPicker.ListDetails(request.TeamId, scope));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return items.Select(i => MdtMapper.ToResponse(i, request.Viewer, now)).ToList();
    }
}
