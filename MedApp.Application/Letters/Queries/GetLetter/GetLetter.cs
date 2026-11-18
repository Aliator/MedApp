using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.PreviewLetterTemplate;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Letters.Queries.GetLetter;

/// <summary>One letter, as its page shows it. Recorded as viewing it (<c>LetterViewed</c>).</summary>
public sealed record GetLetterQuery(Guid LetterId, PathwayActor Viewer) : IRequest<LetterResponse>;

public sealed class GetLetterHandler(LetterRecords records, IActivityLog activityLog, TimeProvider timeProvider)
    : IRequestHandler<GetLetterQuery, LetterResponse>
{
    public async Task<LetterResponse> Handle(GetLetterQuery request, CancellationToken cancellationToken)
    {
        var record = await records.LoadAsync(request.LetterId, forUpdate: false, cancellationToken);

        await activityLog.RecordViewAsync(
            ActivityAction.LetterViewed, record.Letter.PatientId, new { letterId = record.Letter.Id }, cancellationToken);

        return await records.ToResponseAsync(record, request.Viewer, UkTime.Today(timeProvider), cancellationToken);
    }
}

/// <summary>
/// A draft's PDF, as it would be approved now but marked "Draft, not approved" on every page (hazard H-50): for its
/// author, its approver, or anyone who approves letters. Recorded as viewing it (<c>LetterViewed</c>, with
/// <c>preview</c>).
/// </summary>
public sealed record PreviewLetterQuery(Guid LetterId, PathwayActor Viewer) : IRequest<LetterPdf>;

public sealed class PreviewLetterHandler(
    LetterRecords records,
    ILetterRenderer renderer,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<PreviewLetterQuery, LetterPdf>
{
    public const string FileName = "letter-preview.pdf";

    public async Task<LetterPdf> Handle(PreviewLetterQuery request, CancellationToken cancellationToken)
    {
        var record = await records.LoadAsync(request.LetterId, forUpdate: false, cancellationToken);
        var letter = record.Letter;
        if (letter.Status is not (Domain.Letters.LetterStatus.Draft or Domain.Letters.LetterStatus.AwaitingApproval))
            throw new ConflictException("Only a draft has a preview. Download an approved letter's PDF from its page.");
        if (!LetterWorkflow.CanPreview(letter, request.Viewer))
            throw new ForbiddenException("Only the letter's author, its approver, or someone who approves letters can preview a draft.");

        await activityLog.RecordViewAsync(
            ActivityAction.LetterViewed, letter.PatientId, new { letterId = letter.Id, preview = true }, cancellationToken);

        var values = await records.MergeValuesAsync(record, letter.ApproverUserId, UkTime.Today(timeProvider), cancellationToken);
        var filled = LetterRendering.Fill(letter, values, LetterRendering.DraftMarking);
        return new LetterPdf(FileName, await renderer.RenderAsync(filled.Document, cancellationToken));
    }
}

/// <summary>The side nav's count: the caller's letters to approve, and every approved letter still to send. Records nothing.</summary>
public sealed record CountLettersQuery(Guid ViewerId) : IRequest<LetterCountResponse>;

public sealed class CountLettersHandler(LetterRecords records) : IRequestHandler<CountLettersQuery, LetterCountResponse>
{
    public async Task<LetterCountResponse> Handle(CountLettersQuery request, CancellationToken cancellationToken)
    {
        var (toApprove, toSend) = await records.Letters.CountAsync(request.ViewerId, cancellationToken);
        return new LetterCountResponse { ToApprove = toApprove, ToSend = toSend };
    }
}
