using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Commands.UpdateClinicPattern;

public sealed class UpdateClinicPatternHandler(
    IClinicRepository repository,
    ITeamRepository teamRepository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateClinicPatternCommand, ClinicPatternResponse>
{
    public async Task<ClinicPatternResponse> Handle(UpdateClinicPatternCommand request, CancellationToken cancellationToken)
    {
        var pattern = await repository.GetPatternForUpdateAsync(request.PatternId, cancellationToken)
                      ?? throw new NotFoundException($"Clinic pattern '{request.PatternId}' was not found.");
        if (pattern.LastUpdated != UkTime.Utc(request.ExpectedLastUpdated!.Value))
            throw new ConflictException(ClinicRules.PatternChangedMeanwhile);

        var staffUsername = await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        await ClinicChoices.CheckRoomAsync(repository, request.RoomId, today, pattern.RoomId, cancellationToken);
        await ClinicChoices.CheckTeamAsync(teamRepository, request.TeamId, pattern.TeamId, cancellationToken);

        pattern.StaffUserId = request.StaffUserId;
        pattern.RoomId = request.RoomId;
        pattern.TeamId = request.TeamId;
        pattern.DayOfWeek = HoursValidation.Day(request.DayOfWeek);
        pattern.Starts = request.Starts;
        pattern.Ends = request.Ends;
        pattern.SlotMinutes = request.SlotMinutes;
        pattern.EveryWeeks = request.EveryWeeks;
        pattern.FromDate = request.FromDate;
        pattern.UntilDate = request.UntilDate;
        pattern.LastUpdated = now;

        await repository.SaveAsync([], ClinicRules.PatternChangedMeanwhile, cancellationToken);

        var saved = await repository.GetPatternAsync(pattern.Id, cancellationToken) ?? pattern;
        return ClinicMapper.ToResponse(saved, staffUsername, today);
    }
}
