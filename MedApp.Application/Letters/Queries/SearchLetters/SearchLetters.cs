using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Queries.SearchLetters;

/// <summary>The letters list's views (decision 108).</summary>
public enum LetterView
{
    /// <summary>The viewer's drafts, not returned, most recently changed first.</summary>
    MyDrafts,

    /// <summary>Letters submitted to the viewer, oldest submitted first.</summary>
    AwaitingMyApproval,

    /// <summary>The viewer's drafts their approver returned, oldest returned first.</summary>
    ReturnedToMe,

    /// <summary>Approved letters with a recipient not yet sent (or a failed secure email), oldest approved first.</summary>
    ToSend,

    /// <summary>Approved letters sent to every recipient, newest approved first.</summary>
    Sent,
}

/// <summary>
/// One page of letters: a view (<see cref="LetterView"/>) or, without one, every letter of a patient, pathway or
/// referral, newest first. Each patient shown is recorded as listed.
/// </summary>
public sealed record SearchLettersQuery(
    int Page,
    int PageSize,
    string? View,
    Guid? PatientId,
    Guid? PathwayId,
    Guid? ReferralId,
    Guid ViewerId) : IRequest<PagedResponse<LetterSummaryResponse>>;

public sealed class SearchLettersValidator : AbstractValidator<SearchLettersQuery>
{
    public SearchLettersValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.View)
            .Must(view => view is null || PathwayEnums.TryParse<LetterView>(view, out _))
            .WithMessage($"View must be one of: {string.Join(", ", Enum.GetNames<LetterView>())}.");

        RuleFor(x => x)
            .Must(x => x.View is not null || x.PatientId is not null || x.PathwayId is not null || x.ReferralId is not null)
            .WithName("View")
            .WithMessage("Choose a view, or a patient, pathway or referral.");
    }
}

/// <summary>
/// The letters list's filters and orders, in LINQ EF translates, so the same code runs in SQL and in unit tests. Each
/// order ends with the letter's ID, so paging never repeats or skips one. Ordered by dates only (decision 69).
/// </summary>
public static class LetterSearch
{
    public static IQueryable<Letter> Apply(IQueryable<Letter> letters, SearchLettersQuery query)
    {
        if (query.PatientId is { } patientId)
            letters = letters.Where(l => l.PatientId == patientId);
        if (query.PathwayId is { } pathwayId)
            letters = letters.Where(l => l.PathwayId == pathwayId);
        if (query.ReferralId is { } referralId)
            letters = letters.Where(l => l.ReferralId == referralId);

        var viewer = query.ViewerId;
        LetterView? view = query.View is not null && PathwayEnums.TryParse<LetterView>(query.View, out var parsed) ? parsed : null;
        return view switch
        {
            LetterView.MyDrafts => letters
                .Where(l => l.Status == LetterStatus.Draft && l.AuthorUserId == viewer && l.ReturnComment == null)
                .OrderByDescending(l => l.LastUpdated).ThenBy(l => l.Id),
            LetterView.ReturnedToMe => letters
                .Where(l => l.Status == LetterStatus.Draft && l.AuthorUserId == viewer && l.ReturnComment != null)
                .OrderBy(l => l.ReturnedAtUtc).ThenBy(l => l.Id),
            LetterView.AwaitingMyApproval => letters
                .Where(l => l.Status == LetterStatus.AwaitingApproval && l.ApproverUserId == viewer)
                .OrderBy(l => l.SubmittedAtUtc).ThenBy(l => l.Id),
            LetterView.ToSend => ToSend(letters)
                .OrderBy(l => l.ApprovedAtUtc).ThenBy(l => l.Id),
            LetterView.Sent => letters
                .Where(l => l.Status == LetterStatus.Approved
                            && l.Recipients.Any()
                            && l.Recipients.All(r => r.DispatchStatus == LetterDispatchStatus.Sent))
                .OrderByDescending(l => l.ApprovedAtUtc).ThenBy(l => l.Id),
            _ => letters.OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id),
        };
    }

    /// <summary>Approved letters with a recipient not yet sent.</summary>
    public static IQueryable<Letter> ToSend(IQueryable<Letter> letters) =>
        letters.Where(l => l.Status == LetterStatus.Approved
                           && l.Recipients.Any(r => r.DispatchStatus != LetterDispatchStatus.Sent));
}

public sealed class SearchLettersHandler(
    ILetterRepository repository,
    MedApp.Application.Common.Identity.IIdentityReadService identity,
    IActivityLog activityLog)
    : IRequestHandler<SearchLettersQuery, PagedResponse<LetterSummaryResponse>>
{
    public async Task<PagedResponse<LetterSummaryResponse>> Handle(SearchLettersQuery request, CancellationToken cancellationToken)
    {
        var result = await repository.SearchAsync(request, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(row => row.Letter.PatientId), cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync(
            result.Items.SelectMany(row => LetterMapper.PeopleIn(row.Letter)), cancellationToken);
        return PagedMapper.ToResponse(result, request.Page, request.PageSize, row => LetterMapper.ToSummary(
            row.Letter, $"{row.PatientFirstName} {row.PatientLastName}", row.PathwayTitle, usernames));
    }
}
