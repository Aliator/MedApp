using System.Security.Cryptography;
using FluentValidation;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Letters.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Attachments;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Letters.Commands.ApproveLetter;

/// <summary>Approves a letter: its named approver, or its author approving their own draft.</summary>
public sealed record ApproveLetterCommand(Guid LetterId, int ExpectedVersion, PathwayActor Actor) : IRequest<LetterResponse>;

public sealed class ApproveLetterValidator : AbstractValidator<ApproveLetterCommand>
{
    public ApproveLetterValidator()
    {
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Approves a letter (decision 102). In order: who (its named approver while it awaits approval, or its author for
/// their own draft; 403 otherwise), its status and version (409), the record allows letters (409), the approver has a
/// full name (409, decision 80), it has recipients, each with a name and an address (409; copied again now, decision
/// 101), every merge field it uses has a value (409, naming them), and every character can be printed (409). Then its
/// PDF is made, with the patient's identifiers and "Page n of m" on every page (hazard H-50), stored as a file on the
/// pathway (or the referral), and the letter frozen: the file, its audit entry and the letter's are saved together.
/// Its author is told, if someone else approved it.
/// </summary>
public sealed class ApproveLetterHandler(
    LetterRecords records,
    IStaffDetailsService staff,
    ILetterRenderer renderer,
    IFileStore fileStore,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<ApproveLetterCommand, LetterResponse>
{
    public const string PdfContentType = "application/pdf";

    public async Task<LetterResponse> Handle(ApproveLetterCommand request, CancellationToken cancellationToken)
    {
        var record = await records.LoadForChangeAsync(request.LetterId, request.Actor, cancellationToken);
        var letter = record.Letter;
        EnsureMayApprove(letter, request.Actor);
        LetterWorkflow.EnsureVersion(letter, request.ExpectedVersion);
        LetterWorkflow.EnsureRecordAllowsLetters(record.Patient);

        var approver = await staff.GetAsync(request.Actor.UserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(approver?.FullName))
            throw new ConflictException(LetterWorkflow.NoFullNameMessage);

        if (letter.Recipients.Count == 0)
            throw new ConflictException(LetterWorkflow.NoRecipientsMessage);
        if (await records.RefreshRecipientsAsync(record, cancellationToken) is { } recipientProblem)
            throw new ConflictException(recipientProblem);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        var values = await records.MergeValuesAsync(record, request.Actor.UserId, today, cancellationToken);
        var filled = LetterRendering.Fill(letter, values, marking: null);
        if (filled.Missing.Count > 0)
            throw new ConflictException(LetterRendering.MissingMessage(filled.Missing));
        var unsupported = renderer.FindUnsupportedCharacters(LetterRendering.AllText(filled.Document));
        if (unsupported.Count > 0)
            throw new ConflictException(
                $"The letter can't be approved: {LetterTemplateValidation.UnsupportedMessage(unsupported)} " +
                "If it's in the patient's or a recipient's details, ask for them to be checked.");

        var pdf = await renderer.RenderAsync(filled.Document, cancellationToken);
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            PatientId = letter.PatientId,
            TeamId = letter.TeamId,
            PathwayId = letter.PathwayId,
            // A referral's letter is filed on the referral; a pathway's on the pathway.
            ReferralId = letter.PathwayId is null ? letter.ReferralId : null,
            FileName = LetterRendering.FileName(letter, today),
            ContentType = PdfContentType,
            SizeBytes = pdf.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(pdf)),
            // Made by MedApp, not uploaded: no scanner has looked at it.
            ScanStatus = AttachmentScanStatus.NotScanned,
            UploadedByUserId = request.Actor.UserId,
            UploadedByUsername = request.Actor.Username,
            UploadedAtUtc = now,
        };
        var target = new AttachmentTarget(letter.PatientId, record.Pathway, null)
        {
            Patient = record.Patient,
            Referral = record.Pathway is null ? record.Referral : null,
        };
        var fileAudit = AttachmentAudit.Entry(attachment, target, request.Actor, PatientPathwayAuditAction.AttachmentAdded, now);

        // The letter's entry a tick after the file's, so the timeline shows them in order.
        var change = new LetterChange(letter, record.Pathway, request.Actor, now.AddTicks(1));
        change.Approve(attachment.Id);

        await using (var content = new MemoryStream(pdf, writable: false))
            await fileStore.SaveAsync(AttachmentStorageKeys.File(attachment.Id), content, PdfContentType, cancellationToken);
        try
        {
            await records.Letters.SaveApprovalAsync(attachment, [fileAudit, .. change.AuditEntries], cancellationToken);
        }
        catch
        {
            await DeleteQuietlyAsync(attachment.Id);
            throw;
        }

        if (LetterNotifications.Approved(letter, request.Actor, now) is { } approved)
            await notifications.SendAsync(approved, cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, today, cancellationToken);
    }

    /// <summary>403 for someone who may not approve this letter, 409 for one not awaiting approval.</summary>
    public static void EnsureMayApprove(Letter letter, PathwayActor actor)
    {
        switch (letter.Status)
        {
            case LetterStatus.AwaitingApproval when letter.ApproverUserId != actor.UserId:
                throw new ForbiddenException(LetterWorkflow.NotApproverMessage);
            case LetterStatus.Draft when letter.AuthorUserId != actor.UserId:
                throw new ForbiddenException(LetterWorkflow.NotAuthorMessage);
            case LetterStatus.Draft when !actor.Has(Permission.ApproveLetters):
                throw new ForbiddenException(LetterWorkflow.CantApproveOwnMessage);
            case LetterStatus.Approved or LetterStatus.Withdrawn:
                throw new ConflictException(LetterWorkflow.NotInStatusMessage(letter, "approved"));
        }
    }

    private async Task DeleteQuietlyAsync(Guid attachmentId)
    {
        // Not with the request's token (it may be why this failed), but not for ever either.
        using var timeout = new CancellationTokenSource(Attachments.Commands.UploadAttachment.UploadAttachmentHandler.CleanUpTimeout);
        try
        {
            await fileStore.DeleteAsync(AttachmentStorageKeys.File(attachmentId), timeout.Token);
        }
        catch (Exception)
        {
            // Best effort: an orphaned blob wastes space but exposes nothing (only the API reads the store).
        }
    }
}
