using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Queries.GetMeetingById;

public sealed class GetMeetingByIdHandler(
    IMeetingRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetMeetingByIdQuery, MeetingResponse>
{
    public async Task<MeetingResponse> Handle(GetMeetingByIdQuery request, CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(request.MeetingId, cancellationToken)
                      ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");

        var usernames = await identityReadService.GetUsernamesByIdAsync(MeetingMapper.UserIds(meeting), cancellationToken);
        return MeetingMapper.ToResponse(meeting, usernames, request.Actor.IsAdmin, UkTime.Today(timeProvider));
    }
}
