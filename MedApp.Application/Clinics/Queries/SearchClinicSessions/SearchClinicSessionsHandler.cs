using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Queries.SearchClinicSessions;

public sealed class SearchClinicSessionsHandler(
    IClinicRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<SearchClinicSessionsQuery, IReadOnlyList<ClinicSessionResponse>>
{
    public async Task<IReadOnlyList<ClinicSessionResponse>> Handle(SearchClinicSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await repository.SearchSessionsAsync(
            new ClinicSessionFilter(
                request.From,
                request.To,
                request.StaffUserId,
                request.SiteId,
                request.RoomId,
                request.TeamId,
                request.IncludeCancelled && !request.WithFreeSlots),
            cancellationToken);
        if (sessions.Count == 0)
            return [];

        var booked = await repository.GetBookedStartsAsync(sessions.Select(s => s.Id), cancellationToken);
        var usernames = await identityReadService.GetUsernamesByIdAsync(sessions.Select(s => s.StaffUserId).Distinct(), cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return sessions
            .Select(s => ClinicMapper.ToResponse(
                s,
                usernames.GetValueOrDefault(s.StaffUserId) ?? string.Empty,
                booked.GetValueOrDefault(s.Id) ?? [],
                now,
                request.Actor.CanManageClinics))
            .Where(s => !request.WithFreeSlots || s.FreeSlotCount > 0)
            .ToList();
    }
}
