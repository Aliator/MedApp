using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Commands.ChooseMdtPresenter;

/// <summary>
/// Chooses an item's presenter (MC21; the light demo's <c>presenter</c>): only the item's own team (behind the wall a
/// 404, recorded; another team's a 409); the item open (409) and on a meeting (409 when it was cancelled); the presenter
/// one of the meeting's attendees (400). No audit entry or notification (no new stored numbers).
/// </summary>
public sealed class ChooseMdtPresenterHandler(
    IMdtItemRepository repository,
    ITeamBoundary boundary,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<ChooseMdtPresenterCommand, MdtItemResponse>
{
    public async Task<MdtItemResponse> Handle(ChooseMdtPresenterCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetForUpdateAsync(request.ItemId, cancellationToken)
                   ?? throw new NotFoundException(MdtWalls.NotFoundMessage(request.ItemId));
        await boundary.EnsureCanChangeAsync(item, request.Actor.Teams, cancellationToken);

        PatientRules.EnsureCanChange(item.Patient);
        MdtRules.EnsureVersion(item, request.ExpectedVersion);
        MdtRules.EnsureOpen(item);
        if (item.MeetingId is null || item.Meeting is null)
            throw new ConflictException(MdtRules.NoMeetingMessage);

        string? username = null;
        if (request.PresenterUserId is { } presenterId)
        {
            if (item.Meeting.Attendees.All(a => a.StaffUserId != presenterId))
                throw new ValidationException([
                    new ValidationFailure(nameof(request.PresenterUserId), MdtRules.PresenterNotAttendingMessage)
                ]);
            var usernames = await identityReadService.GetUsernamesByIdAsync([presenterId], cancellationToken);
            username = usernames.GetValueOrDefault(presenterId) ?? PathwayMapper.DeletedUser;
        }

        item.PresenterUserId = request.PresenterUserId;
        item.PresenterUsername = username;
        item.Version++;
        await repository.SaveAsync(cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await repository.ToResponseAsync(item, request.Actor, now, cancellationToken);
    }
}
