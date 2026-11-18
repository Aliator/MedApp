using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Practices.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Letters.Commands.UpdateLetter;

/// <summary>Saves a draft's text, large print and recipients, from the version its author saw.</summary>
public sealed record UpdateLetterCommand(
    Guid LetterId,
    string Letterhead,
    string Body,
    string Closing,
    bool LargePrint,
    IReadOnlyList<LetterRecipientCommandItem> Recipients,
    int ExpectedVersion,
    PathwayActor Actor) : IRequest<LetterResponse>;

public sealed class UpdateLetterValidator : AbstractValidator<UpdateLetterCommand>
{
    public UpdateLetterValidator(ILetterRenderer renderer, IOptions<LettersOptions> options)
    {
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
        this.Text(renderer, x => x.Letterhead, "Letterhead", Letter.MaxLetterheadLength);
        this.Text(renderer, x => x.Body, "Body", Letter.MaxBodyLength);
        this.Text(renderer, x => x.Closing, "Closing", Letter.MaxClosingLength);
        this.Recipients(renderer, options.Value, x => x.Recipients);
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Saves a draft: only its author (403), only while it's a draft (409), from the version saved now (409), on a record
/// that allows letters (409). Drafts aren't audited edit by edit (decision 100); the version moves on.
/// </summary>
public sealed class UpdateLetterHandler(LetterRecords records, TimeProvider timeProvider)
    : IRequestHandler<UpdateLetterCommand, LetterResponse>
{
    public async Task<LetterResponse> Handle(UpdateLetterCommand request, CancellationToken cancellationToken)
    {
        var record = await records.LoadAsync(request.LetterId, forUpdate: true, cancellationToken);
        var letter = record.Letter;
        if (letter.AuthorUserId != request.Actor.UserId)
            throw new ForbiddenException(LetterWorkflow.NotAuthorMessage);
        if (letter.Status != LetterStatus.Draft)
            throw new ConflictException(LetterWorkflow.NotInStatusMessage(letter, "edited"));
        LetterWorkflow.EnsureVersion(letter, request.ExpectedVersion);
        LetterWorkflow.EnsureRecordAllowsLetters(record.Patient);

        var recipients = await records.RecipientsAsync(
            record, request.Recipients.Select(r => r.ToInput()).ToList(), cancellationToken);

        letter.Letterhead = LetterTemplateRules.Tidy(request.Letterhead);
        letter.Body = LetterTemplateRules.Tidy(request.Body);
        letter.Closing = LetterTemplateRules.Tidy(request.Closing);
        letter.LargePrint = request.LargePrint;
        records.Letters.ReplaceRecipients(letter, recipients);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        new LetterChange(letter, record.Pathway, request.Actor, now).Edit();
        await records.Letters.SaveAsync([], cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, UkTime.DateOf(now), cancellationToken);
    }
}
