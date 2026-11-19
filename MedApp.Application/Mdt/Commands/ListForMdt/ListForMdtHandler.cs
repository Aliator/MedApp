using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Mdt;
using MediatR;

namespace MedApp.Application.Mdt.Commands.ListForMdt;

/// <summary>
/// Lists a pathway for an MDT. Only the pathway's own team lists it (on any team's MDT): behind the viewer's team wall a
/// 404 (recorded), seen but another team's a 409 (<see cref="TeamWalls.NotYourTeams"/>).
/// </summary>
public sealed class ListForMdtHandler(
    IMdtItemRepository repository,
    IPatientPathwayRepository pathways,
    ITeamBoundary boundary,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<ListForMdtCommand, MdtItemResponse>
{
    public async Task<MdtItemResponse> Handle(ListForMdtCommand request, CancellationToken cancellationToken)
    {
        var pathway = await pathways.GetByIdAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException(PathwayRepositoryExtensions.NotFoundMessage(request.PathwayId));
        await boundary.EnsureCanChangePathwayAsync(
            pathway, request.Actor.Teams, PathwayRepositoryExtensions.NotFoundMessage(request.PathwayId), cancellationToken);

        // New work: refused for a restricted or anonymised record (decision 75).
        PatientRules.EnsureCanChange(pathway.Patient);
        MdtRules.EnsurePathwayCanBeListed(pathway);

        var meeting = await repository.GetMeetingAsync(request.MeetingId, cancellationToken)
                      ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        MdtRules.EnsureCanListFor(meeting, now);

        if (await repository.IsOpenOnMeetingAsync(pathway.Id, meeting.Id, cancellationToken))
            throw new ConflictException(MdtRules.AlreadyListedMessage);

        var team = await teams.GetAsync(meeting.TeamId!.Value, cancellationToken);
        var item = new MdtItem
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            PathwayId = pathway.Id,
            TeamId = meeting.TeamId.Value,
            MeetingId = meeting.Id,
            Question = request.Question.Trim(),
            Status = MdtItemStatus.Listed,
            ListedByUserId = request.Actor.UserId,
            ListedByUsername = request.Actor.Username,
            ListedAtUtc = now,
            Version = 1
        };

        await repository.AddAsync(
            item, MdtAudit.Listed(item, pathway, team?.Name ?? string.Empty, meeting, request.Actor, now), cancellationToken);

        var saved = await repository.GetAsync(item.Id, cancellationToken)
                    ?? throw new NotFoundException($"MDT item '{item.Id}' was not found.");
        return MdtMapper.ToResponse(saved, request.Actor, now);
    }
}
