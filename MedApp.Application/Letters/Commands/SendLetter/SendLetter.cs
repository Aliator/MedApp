using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Practices.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Letters.Commands.SendLetter;

/// <summary>Finds an approved letter's recipient to send to: 404 for one it hasn't, 409 for one it has reached.</summary>
internal static class Sending
{
    public static LetterRecipient RecipientOrThrow(Letter letter, Guid recipientId)
    {
        var recipient = letter.Recipients.FirstOrDefault(r => r.Id == recipientId)
                        ?? throw new NotFoundException($"Recipient '{recipientId}' was not found on this letter.");
        if (letter.Status != LetterStatus.Approved)
            throw new ConflictException(letter.Status == LetterStatus.Withdrawn
                ? "This letter was withdrawn, so it can't be sent."
                : "Only an approved letter can be sent. It's sent as its approved PDF.");
        if (recipient.DispatchStatus == LetterDispatchStatus.Sent)
            throw new ConflictException($"{LetterRecords.Describe(recipient)} has been sent this letter already. Reload to see when and how.");
        return recipient;
    }
}

/// <summary>Sends an approved letter to one recipient by secure email from MedApp.</summary>
public sealed record SendLetterCommand(Guid LetterId, Guid RecipientId, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LetterResponse>;

public sealed class SendLetterValidator : AbstractValidator<SendLetterCommand>
{
    public SendLetterValidator()
    {
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
        RuleFor(x => x.RecipientId).NotEmpty().WithMessage("Recipient id is required.");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Sends a letter by secure email (decision 103): approved and not yet sent to them (409), from the version saved now
/// (409), on a record that allows letters (409), when the relay is set up and declared, to a recipient (not the
/// patient) whose address is in an allowed domain (409, saying why). The letter is claimed first (its row version), so
/// two people sending at once don't send it twice. It's sent synchronously: the recipient is marked sent only when the
/// mail server accepts it; if it doesn't, the attempt is recorded as failed and the sender told at once (409), so it
/// can be tried again or sent another way.
/// </summary>
public sealed class SendLetterHandler(
    LetterRecords records,
    ILetterTransport transport,
    IFileStore fileStore,
    IOptions<LettersOptions> options,
    TimeProvider timeProvider,
    ILogger<SendLetterHandler> logger)
    : IRequestHandler<SendLetterCommand, LetterResponse>
{
    public const string FailedMessage =
        "The secure email server didn't accept the letter, so it hasn't been sent. Try again later, or send it another way and record that.";

    public const string PdfMissingMessage =
        "The letter's PDF can't be found in file storage, so it can't be emailed. Ask an admin to check the file store.";

    public async Task<LetterResponse> Handle(SendLetterCommand request, CancellationToken cancellationToken)
    {
        var record = await records.LoadAsync(request.LetterId, forUpdate: true, cancellationToken);
        var letter = record.Letter;
        var recipient = Sending.RecipientOrThrow(letter, request.RecipientId);
        LetterWorkflow.EnsureVersion(letter, request.ExpectedVersion);
        LetterWorkflow.EnsureRecordAllowsLetters(record.Patient);
        if (LetterWorkflow.WhyNoSecureEmail(recipient, transport.IsAvailable, options.Value.AllowedSecureEmailDomains) is { } why)
            throw new ConflictException(why);

        byte[] pdf;
        await using (var stored = await fileStore.OpenReadAsync(AttachmentStorageKeys.File(letter.PdfAttachmentId!.Value), cancellationToken)
                                  ?? throw new ConflictException(PdfMissingMessage))
        using (var copy = new MemoryStream())
        {
            await stored.CopyToAsync(copy, cancellationToken);
            pdf = copy.ToArray();
        }

        // Claim it: a second sender at the same moment fails here (the row version), before anything is emailed.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        letter.Version++;
        letter.LastUpdated = now;
        await records.Letters.SaveAsync([], cancellationToken);

        var change = new LetterChange(letter, record.Pathway, request.Actor, now);
        var today = UkTime.DateOf(now);
        try
        {
            var reference = await transport.SendAsync(
                SecureLetterEmails.For(recipient.Email!, options.Value.SecureEmail.CentreName, pdf), cancellationToken);
            change.Send(recipient, LetterDispatchMethod.SecureEmail, LetterDispatchStatus.Sent, today,
                Truncate(reference, LetterRecipient.MaxTransportReferenceLength));
            await records.Letters.SaveAsync(change.AuditEntries, cancellationToken);
        }
        catch (LetterTransportException e)
        {
            // Never the address or the server's words: they can quote it.
            logger.LogWarning("A letter's secure email wasn't accepted ({Category}, {ExceptionType}).",
                e.Category, e.InnerException?.GetType().Name ?? nameof(LetterTransportException));
            change.Send(recipient, LetterDispatchMethod.SecureEmail, LetterDispatchStatus.Failed, null);
            await records.Letters.SaveAsync(change.AuditEntries, CancellationToken.None);
            throw new ConflictException(FailedMessage);
        }

        return await records.ToResponseAsync(record, request.Actor, today, cancellationToken);
    }

    private static string? Truncate(string? text, int max) =>
        text is null ? null : text.Length <= max ? text : text[..max];
}

/// <summary>Records an approved letter as sent to one recipient another way.</summary>
public sealed record RecordLetterSentCommand(
    Guid LetterId, Guid RecipientId, string Method, DateOnly SentOn, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LetterResponse>;

public sealed class RecordLetterSentValidator : AbstractValidator<RecordLetterSentCommand>
{
    public RecordLetterSentValidator()
    {
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
        RuleFor(x => x.RecipientId).NotEmpty().WithMessage("Recipient id is required.");
        RuleFor(x => x.Method)
            .Must(m => PathwayEnums.TryParse<LetterDispatchMethod>(m, out var method) && LetterWorkflow.RecordedMethods.Contains(method))
            .WithMessage($"Method must be one of: {string.Join(", ", LetterWorkflow.RecordedMethods)}.");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Records a sending (decision 103): approved and not yet sent to them (409), from the version saved now (409), on a
/// record that allows letters (409; a restricted record's letters wait, asking an admin), on a UK date from the day it
/// was approved, up to 7 days back and not in the future (400). Handed over is for the patient only, and the patient
/// isn't emailed (400, decision 107).
/// </summary>
public sealed class RecordLetterSentHandler(LetterRecords records, TimeProvider timeProvider)
    : IRequestHandler<RecordLetterSentCommand, LetterResponse>
{
    public async Task<LetterResponse> Handle(RecordLetterSentCommand request, CancellationToken cancellationToken)
    {
        var record = await records.LoadAsync(request.LetterId, forUpdate: true, cancellationToken);
        var letter = record.Letter;
        var recipient = Sending.RecipientOrThrow(letter, request.RecipientId);
        LetterWorkflow.EnsureVersion(letter, request.ExpectedVersion);
        LetterWorkflow.EnsureRecordAllowsLetters(record.Patient);

        var method = PathwayEnums.Parse<LetterDispatchMethod>(request.Method);
        if (method == LetterDispatchMethod.HandedToPatient && recipient.Kind != LetterRecipientKind.Patient)
            throw Invalid(nameof(request.Method), "Only the patient can be handed the letter. Choose how it reached this recipient.");
        if (method == LetterDispatchMethod.EmailOutsideMedApp && recipient.Kind == LetterRecipientKind.Patient)
            throw Invalid(nameof(request.Method), LetterWorkflow.PatientEmailMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        var approvedOn = UkTime.DateOf(letter.ApprovedAtUtc!.Value);
        if (request.SentOn > today)
            throw Invalid(nameof(request.SentOn), "The date it was sent can't be in the future.");
        if (request.SentOn < today.AddDays(-LetterWorkflow.MaxDaysBack))
            throw Invalid(nameof(request.SentOn), $"The date it was sent must be within the last {LetterWorkflow.MaxDaysBack} days.");
        if (request.SentOn < approvedOn)
            throw Invalid(nameof(request.SentOn), "The date it was sent can't be before the letter was approved.");

        var change = new LetterChange(letter, record.Pathway, request.Actor, now);
        change.Send(recipient, method, LetterDispatchStatus.Sent, request.SentOn);
        await records.Letters.SaveAsync(change.AuditEntries, cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, today, cancellationToken);
    }

    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
