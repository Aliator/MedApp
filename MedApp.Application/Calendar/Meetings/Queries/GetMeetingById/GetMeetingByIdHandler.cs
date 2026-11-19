using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Queries.GetMeetingById;

public sealed class GetMeetingByIdHandler(
    IMeetingRepository repository,
    IIdentityReadService identityReadService,
    ICalendarRepository calendarRepository,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<GetMeetingByIdQuery, MeetingResponse>
{
    public async Task<MeetingResponse> Handle(GetMeetingByIdQuery request, CancellationToken cancellationToken)
    {
        var meeting = await repository.GetAsync(request.MeetingId, cancellationToken)
                      ?? throw new NotFoundException(MeetingWalls.NotFoundMessage(request.MeetingId));
        // Another team's meeting is busy time only on calendars; opening it is a 404, as if it weren't there (MC11).
        await MeetingWalls.EnsureVisibleAsync(meeting, request.Actor.Teams, calendarRepository, boundary, cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync(MeetingMapper.UserIds(meeting), cancellationToken);
        return MeetingMapper.ToResponse(meeting, usernames, request.Actor, timeProvider.GetUtcNow().UtcDateTime);
    }
}
