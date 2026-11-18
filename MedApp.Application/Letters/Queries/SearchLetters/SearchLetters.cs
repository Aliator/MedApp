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
using MedApp.Domain.Patients;
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

    /// <summary>Every letter, newest started first (the list with its view filter removed).</summary>
    All,
}

/// <summary>
/// One page of letters: a view (<see cref="LetterView"/>) or, without one, every letter of a patient, pathway or
/// referral, newest first, optionally narrowed to a patient's name (<see cref="Search"/>). <see cref="Sort"/> names a
/// column (<see cref="LetterSearch.SortColumns"/>); without one, the view's own order. Each patient shown is recorded
/// as listed.
/// </summary>
public sealed record SearchLettersQuery(
    int Page,
    int PageSize,
    string? View,
    Guid? PatientId,
    Guid? PathwayId,
    Guid? ReferralId,
    Guid ViewerId,
    string? Search = null,
    string? Sort = null,
    bool Ascending = true) : IRequest<PagedResponse<LetterSummaryResponse>>;

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

        RuleFor(x => x.Search)
            .MaximumLength(LetterSearch.MaxSearchLength)
            .WithMessage($"Search must not exceed {LetterSearch.MaxSearchLength} characters.");
    }
}

/// <summary>
/// The letters list's filters and orders, in LINQ EF translates, so the same code runs in SQL and in unit tests. Each
/// order ends with the letter's ID, so paging never repeats or skips one. A view's own order is by dates only
/// (decision 69); the person may sort by a column instead.
/// </summary>
public static class LetterSearch
{
    public const int MaxSearchLength = 200;

    public const string SortByPatient = "patient";

    /// <summary>The kind, then the template's name.</summary>
    public const string SortByLetter = "letter";

    public const string SortByStatus = "status";

    /// <summary>The latest step: withdrawn, approved, submitted (while awaiting approval) or last changed.</summary>
    public const string SortByLatest = "latest";

    public static readonly IReadOnlyList<string> SortColumns = [SortByPatient, SortByLetter, SortByStatus, SortByLatest];

    /// <summary>
    /// The query's letters in its order. <paramref name="patients"/> (letters hold only the patient's ID) is what the
    /// patient's name is searched and sorted by; without it, those are left out.
    /// </summary>
    public static IQueryable<Letter> Apply(
        IQueryable<Letter> letters, SearchLettersQuery query, IQueryable<Patient>? patients = null)
    {
        if (query.PatientId is { } patientId)
            letters = letters.Where(l => l.PatientId == patientId);
        if (query.PathwayId is { } pathwayId)
            letters = letters.Where(l => l.PathwayId == pathwayId);
        if (query.ReferralId is { } referralId)
            letters = letters.Where(l => l.ReferralId == referralId);
        if (patients is not null && !string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            letters = letters.Where(l => patients.Any(p =>
                p.Id == l.PatientId && (p.FirstName + " " + p.LastName).ToLower().Contains(term)));
        }

        var viewer = query.ViewerId;
        LetterView? view = query.View is not null && PathwayEnums.TryParse<LetterView>(query.View, out var parsed) ? parsed : null;
        letters = view switch
        {
            LetterView.MyDrafts => letters
                .Where(l => l.Status == LetterStatus.Draft && l.AuthorUserId == viewer && l.ReturnComment == null),
            LetterView.ReturnedToMe => letters
                .Where(l => l.Status == LetterStatus.Draft && l.AuthorUserId == viewer && l.ReturnComment != null),
            LetterView.AwaitingMyApproval => letters
                .Where(l => l.Status == LetterStatus.AwaitingApproval && l.ApproverUserId == viewer),
            LetterView.ToSend => ToSend(letters),
            LetterView.Sent => letters
                .Where(l => l.Status == LetterStatus.Approved
                            && l.Recipients.Any()
                            && l.Recipients.All(r => r.DispatchStatus == LetterDispatchStatus.Sent)),
            _ => letters,
        };

        var ascending = query.Ascending;
        var ordered = Ordering.ColumnOf(query.Sort, SortColumns) switch
        {
            SortByPatient when patients is not null => letters
                .OrderByDirection(l => patients.Where(p => p.Id == l.PatientId).Select(p => p.LastName).FirstOrDefault(), ascending)
                .ThenByDirection(l => patients.Where(p => p.Id == l.PatientId).Select(p => p.FirstName).FirstOrDefault(), ascending),
            SortByLetter => letters.OrderByDirection(l => l.Kind, ascending).ThenByDirection(l => l.TemplateName, ascending),
            SortByStatus => letters.OrderByDirection(l => l.Status, ascending),
            SortByLatest => letters.OrderByDirection(
                l => l.WithdrawnAtUtc
                     ?? l.ApprovedAtUtc
                     ?? (l.Status == LetterStatus.AwaitingApproval ? l.SubmittedAtUtc : null)
                     ?? l.LastUpdated,
                ascending),
            _ => null,
        };
        if (ordered is not null)
            return ordered.ThenBy(l => l.Id);

        return view switch
        {
            LetterView.MyDrafts => letters.OrderByDescending(l => l.LastUpdated).ThenBy(l => l.Id),
            LetterView.ReturnedToMe => letters.OrderBy(l => l.ReturnedAtUtc).ThenBy(l => l.Id),
            LetterView.AwaitingMyApproval => letters.OrderBy(l => l.SubmittedAtUtc).ThenBy(l => l.Id),
            LetterView.ToSend => letters.OrderBy(l => l.ApprovedAtUtc).ThenBy(l => l.Id),
            LetterView.Sent => letters.OrderByDescending(l => l.ApprovedAtUtc).ThenBy(l => l.Id),
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
