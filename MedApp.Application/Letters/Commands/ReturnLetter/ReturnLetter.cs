using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Commands.ReturnLetter;

/// <summary>Returns a letter awaiting approval to its author, with a comment.</summary>
public sealed record ReturnLetterCommand(Guid LetterId, string Comment, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LetterResponse>;

public sealed class ReturnLetterValidator : AbstractValidator<ReturnLetterCommand>
{
    public ReturnLetterValidator()
    {
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
        RuleFor(x => x.Comment).Comment("Comment");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Returns a letter (decision 102): only its named approver (403), only while it awaits approval (409), from the
/// version saved now (409), not on an anonymised record (409; a restricted one's can go back to its author). The
/// author is told (<c>LetterReturned</c>), without the comment.
/// </summary>
public sealed class ReturnLetterHandler(LetterRecords records, INotificationSender notifications, TimeProvider timeProvider)
    : IRequestHandler<ReturnLetterCommand, LetterResponse>
{
    public async Task<LetterResponse> Handle(ReturnLetterCommand request, CancellationToken cancellationToken)
    {
        var record = await records.LoadForChangeAsync(request.LetterId, request.Actor, cancellationToken);
        var letter = record.Letter;
        if (letter.Status != LetterStatus.AwaitingApproval)
            throw new ConflictException(LetterWorkflow.NotInStatusMessage(letter, "returned"));
        if (letter.ApproverUserId != request.Actor.UserId)
            throw new ForbiddenException(LetterWorkflow.NotApproverMessage);
        LetterWorkflow.EnsureVersion(letter, request.ExpectedVersion);
        PatientRules.EnsureCanPause(record.Patient);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new LetterChange(letter, record.Pathway, request.Actor, now);
        change.Return(request.Comment.Trim());
        await records.Letters.SaveAsync(change.AuditEntries, cancellationToken);

        await notifications.SendAsync(LetterNotifications.Returned(letter, request.Actor, now), cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, UkTime.DateOf(now), cancellationToken);
    }
}
