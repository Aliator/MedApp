using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Domain.Clinics;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CreateClinicPattern;

public sealed class CreateClinicPatternHandler(
    IClinicRepository repository,
    ITeamRepository teamRepository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<CreateClinicPatternCommand, ClinicPatternResponse>
{
    public async Task<ClinicPatternResponse> Handle(CreateClinicPatternCommand request, CancellationToken cancellationToken)
    {
        var staffUsername = await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        await ClinicChoices.CheckRoomAsync(repository, request.RoomId, today, null, cancellationToken);
        await ClinicChoices.CheckTeamAsync(teamRepository, request.TeamId, null, cancellationToken);

        var pattern = new ClinicPattern
        {
            Id = Guid.NewGuid(),
            StaffUserId = request.StaffUserId,
            RoomId = request.RoomId,
            TeamId = request.TeamId,
            DayOfWeek = HoursValidation.Day(request.DayOfWeek),
            Starts = request.Starts,
            Ends = request.Ends,
            SlotMinutes = request.SlotMinutes,
            EveryWeeks = request.EveryWeeks,
            FromDate = request.FromDate,
            UntilDate = request.UntilDate,
            CreatedAtUtc = now,
            CreatedByUserId = request.Actor.UserId,
            LastUpdated = now
        };

        await repository.AddPatternAsync(pattern, cancellationToken);

        var saved = await repository.GetPatternAsync(pattern.Id, cancellationToken) ?? pattern;
        return ClinicMapper.ToResponse(saved, staffUsername, today);
    }
}
