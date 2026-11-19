using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetMdtMeetingList;

public sealed class GetMdtMeetingListHandler(
    IMdtItemRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetMdtMeetingListQuery, MdtMeetingListResponse>
{
    public async Task<MdtMeetingListResponse> Handle(GetMdtMeetingListQuery request, CancellationToken cancellationToken)
    {
        var scope = request.Viewer.Teams;
        var row = await repository.GetMeetingRowAsync(request.MeetingId, scope, cancellationToken)
                  ?? throw new NotFoundException($"MDT '{request.MeetingId}' was not found.");
        var items = await repository.GetForMeetingAsync(request.MeetingId, scope, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, items.Select(i => i.PatientId).Distinct(), cancellationToken,
            details: TeamPicker.ListDetails(row.Meeting.TeamId, scope));

        var usernames = await identityReadService.GetUsernamesByIdAsync(
            row.Meeting.Attendees.Select(a => a.StaffUserId), cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new MdtMeetingListResponse
        {
            Meeting = MdtMapper.ToResponse(row, usernames),
            Items = items.Select(i => MdtMapper.ToResponse(i, request.Viewer, now)).ToList()
        };
    }
}
