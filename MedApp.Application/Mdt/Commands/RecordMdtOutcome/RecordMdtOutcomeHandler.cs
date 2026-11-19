using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Common.Identity;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Mdt;
using MediatR;

namespace MedApp.Application.Mdt.Commands.RecordMdtOutcome;

public sealed class RecordMdtOutcomeHandler(
    IMdtItemRepository repository,
    ITeamBoundary boundary,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<RecordMdtOutcomeCommand, MdtItemResponse>
{
    public async Task<MdtItemResponse> Handle(RecordMdtOutcomeCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetForUpdateAsync(request.ItemId, cancellationToken)
                   ?? throw new NotFoundException(MdtWalls.NotFoundMessage(request.ItemId));
        // Whoever sees it may record its outcome (by RecordMdtOutcomes): behind the wall a 404 (recorded).
        await boundary.EnsureVisibleAsync(item, request.Actor.Teams, cancellationToken);

        PatientRules.EnsureCanPause(item.Patient);
        MdtRules.EnsureVersion(item, request.ExpectedVersion);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        MdtRules.EnsureCanRecordOutcome(item, now);

        // Only the meeting's attendees can have been present.
        var invited = item.Meeting!.Attendees.Select(a => a.StaffUserId).ToHashSet();
        var present = request.PresentUserIds.Distinct().ToList();
        if (present.Any(id => !invited.Contains(id)))
            throw new ValidationException([new ValidationFailure(nameof(request.PresentUserIds), MdtRules.NotPresentMessage)]);
        var usernames = await identityReadService.GetUsernamesByIdAsync(present, cancellationToken);

        item.Status = MdtItemStatus.Discussed;
        item.Outcome = request.Outcome.Trim();
        item.OutcomeByUserId = request.Actor.UserId;
        item.OutcomeByUsername = request.Actor.Username;
        item.OutcomeAtUtc = now;
        item.Attendees = present
            .Select(id => new MdtItemAttendee
            {
                MdtItemId = item.Id,
                StaffUserId = id,
                Username = usernames.GetValueOrDefault(id) ?? "Deleted user"
            })
            .ToList();
        item.Version++;

        // Its attendees keep reading it until the meeting ends (MC26); after that, their grants end now.
        await repository.SyncGrantsAsync([item], now, cancellationToken);
        await repository.SaveAsync(
            MdtAudit.OutcomeRecorded(item, item.Team?.Name ?? string.Empty, request.Actor, now), cancellationToken);

        foreach (var notification in MdtNotifications.OutcomeRecorded(item, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        return await repository.ToResponseAsync(item, request.Actor, now, cancellationToken);
    }
}
