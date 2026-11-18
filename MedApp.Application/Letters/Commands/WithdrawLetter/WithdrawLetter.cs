using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Commands.WithdrawLetter;

/// <summary>Withdraws an approved letter found wrong, with a reason.</summary>
public sealed record WithdrawLetterCommand(Guid LetterId, string Reason, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LetterResponse>;

public sealed class WithdrawLetterValidator : AbstractValidator<WithdrawLetterCommand>
{
    public WithdrawLetterValidator()
    {
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
        RuleFor(x => x.Reason).Comment("Reason");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Withdraws an approved letter (decision 104): only once it's approved (409), by whoever approved it or someone who
/// can correct records (403), from the version saved now (409). It protects the patient, so a restricted record's can
/// be withdrawn; an anonymised one's can't (409). Its PDF stays on file; the page lists who it already reached, to
/// tell them, and offers a correction.
/// </summary>
public sealed class WithdrawLetterHandler(LetterRecords records, TimeProvider timeProvider)
    : IRequestHandler<WithdrawLetterCommand, LetterResponse>
{
    public async Task<LetterResponse> Handle(WithdrawLetterCommand request, CancellationToken cancellationToken)
    {
        var record = await records.LoadAsync(request.LetterId, forUpdate: true, cancellationToken);
        var letter = record.Letter;
        if (letter.Status != LetterStatus.Approved)
            throw new ConflictException(letter.Status == LetterStatus.Withdrawn
                ? LetterWorkflow.NotInStatusMessage(letter, "withdrawn again")
                : "Only an approved letter can be withdrawn. Its author can discard a draft.");
        if (!LetterWorkflow.CanWithdraw(letter, request.Actor))
            throw new ForbiddenException(LetterWorkflow.NotWithdrawerMessage);
        LetterWorkflow.EnsureVersion(letter, request.ExpectedVersion);
        PatientRules.EnsureCanPause(record.Patient);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new LetterChange(letter, record.Pathway, request.Actor, now);
        change.Withdraw(request.Reason.Trim());
        await records.Letters.SaveAsync(change.AuditEntries, cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, UkTime.DateOf(now), cancellationToken);
    }
}

/// <summary>Discards its author's draft.</summary>
public sealed record DiscardLetterCommand(Guid LetterId, int ExpectedVersion, PathwayActor Actor) : IRequest<LetterResponse>;

public sealed class DiscardLetterValidator : AbstractValidator<DiscardLetterCommand>
{
    public DiscardLetterValidator()
    {
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Discards a draft: only its author (403), only a draft (409; one awaiting approval goes back to its author first),
/// from the version saved now (409), not on an anonymised record (409). It's withdrawn with no reason, so a draft never
/// sent doesn't stay open for ever (an open draft stops anonymisation).
/// </summary>
public sealed class DiscardLetterHandler(LetterRecords records, TimeProvider timeProvider)
    : IRequestHandler<DiscardLetterCommand, LetterResponse>
{
    public async Task<LetterResponse> Handle(DiscardLetterCommand request, CancellationToken cancellationToken)
    {
        var record = await records.LoadAsync(request.LetterId, forUpdate: true, cancellationToken);
        var letter = record.Letter;
        if (letter.AuthorUserId != request.Actor.UserId)
            throw new ForbiddenException(LetterWorkflow.NotAuthorMessage);
        if (letter.Status != LetterStatus.Draft)
            throw new ConflictException(LetterWorkflow.NotInStatusMessage(letter, "discarded"));
        LetterWorkflow.EnsureVersion(letter, request.ExpectedVersion);
        PatientRules.EnsureCanPause(record.Patient);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new LetterChange(letter, record.Pathway, request.Actor, now);
        change.Withdraw(null);
        await records.Letters.SaveAsync(change.AuditEntries, cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, UkTime.DateOf(now), cancellationToken);
    }
}
