using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Letters.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Commands.SubmitLetter;

/// <summary>Submits a draft to a named holder of ApproveLetters, who is told.</summary>
public sealed record SubmitLetterCommand(Guid LetterId, Guid ApproverUserId, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LetterResponse>;

public sealed class SubmitLetterValidator : AbstractValidator<SubmitLetterCommand>
{
    public SubmitLetterValidator()
    {
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
        RuleFor(x => x.ApproverUserId).NotEmpty().WithMessage("Choose who should approve the letter.");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Submits a draft (decision 102): only its author (403), only a draft (409), from the version saved now (409), on a
/// record that allows letters (409), with at least one recipient (409), to someone else who holds ApproveLetters (400).
/// The approver is told (<c>LetterAwaitingApproval</c>).
/// </summary>
public sealed class SubmitLetterHandler(
    LetterRecords records,
    IPermissionHolders permissionHolders,
    IIdentityReadService identity,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitLetterCommand, LetterResponse>
{
    public const string NotAnApproverMessage =
        "That person can't approve letters (their roles don't give them the Approve letters permission). Choose someone who can.";

    public async Task<LetterResponse> Handle(SubmitLetterCommand request, CancellationToken cancellationToken)
    {
        var record = await records.LoadAsync(request.LetterId, forUpdate: true, cancellationToken);
        var letter = record.Letter;
        if (letter.AuthorUserId != request.Actor.UserId)
            throw new ForbiddenException(LetterWorkflow.NotAuthorMessage);
        if (letter.Status != LetterStatus.Draft)
            throw new ConflictException(LetterWorkflow.NotInStatusMessage(letter, "submitted"));
        LetterWorkflow.EnsureVersion(letter, request.ExpectedVersion);
        LetterWorkflow.EnsureRecordAllowsLetters(record.Patient);
        if (letter.Recipients.Count == 0)
            throw new ConflictException(LetterWorkflow.NoRecipientsMessage);

        if (request.ApproverUserId == request.Actor.UserId)
            throw Invalid(LetterWorkflow.SubmitToSelfMessage);
        var holders = await permissionHolders.WhoHoldsAsync([request.ApproverUserId], Permission.ApproveLetters, cancellationToken);
        if (!holders.Contains(request.ApproverUserId))
            throw Invalid(NotAnApproverMessage);
        var usernames = await identity.GetUsernamesByIdAsync([request.ApproverUserId], cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new LetterChange(letter, record.Pathway, request.Actor, now);
        change.Submit(request.ApproverUserId, usernames.GetValueOrDefault(request.ApproverUserId, string.Empty));
        await records.Letters.SaveAsync(change.AuditEntries, cancellationToken);

        await notifications.SendAsync(LetterNotifications.AwaitingApproval(letter, request.Actor, now), cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, UkTime.DateOf(now), cancellationToken);
    }

    private static ValidationException Invalid(string message) =>
        new([new ValidationFailure(nameof(SubmitLetterCommand.ApproverUserId), message)]);
}
