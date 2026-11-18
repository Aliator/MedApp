using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Domain.Clinics;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CreateClinicSession;

public sealed class CreateClinicSessionHandler(
    IClinicRepository repository,
    ICalendarRepository calendarRepository,
    ITeamRepository teamRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<CreateClinicSessionCommand, ClinicSessionResponse>
{
    public async Task<ClinicSessionResponse> Handle(CreateClinicSessionCommand request, CancellationToken cancellationToken)
    {
        var staffUsername = await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);
        await ClinicChoices.CheckTeamAsync(teamRepository, request.TeamId, null, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);

        var session = new ClinicSession
        {
            Id = Guid.NewGuid(),
            StaffUserId = request.StaffUserId,
            RoomId = request.RoomId,
            TeamId = request.TeamId,
            Date = UkTime.DateOf(start),
            StartUtc = start,
            EndUtc = end,
            SlotMinutes = request.SlotMinutes,
            Status = ClinicSessionStatus.Planned,
            CreatedAtUtc = now,
            CreatedByUserId = request.Actor.UserId,
            LastUpdated = now
        };

        // Holding the clinician's and the room's locks, so neither can be taken twice at once.
        await calendarLock.RunAsync(SessionPlacement.Locks(request.StaffUserId, request.RoomId), async ct =>
        {
            await ClinicChoices.CheckRoomAsync(repository, request.RoomId, session.Date, null, ct);
            if (await SessionPlacement.WhyNotAsync(calendarRepository, repository, request.StaffUserId, request.RoomId, start, end, ct) is { } why)
                throw new ConflictException(why);

            await repository.AddSessionAsync(session, ct);
            return session.Id;
        }, cancellationToken);

        var saved = await repository.GetSessionAsync(session.Id, cancellationToken) ?? session;
        return ClinicMapper.ToResponse(saved, staffUsername, [], now, request.Actor.CanManageClinics);
    }
}
