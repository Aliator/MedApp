using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Results.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Results;
using MediatR;
using static MedApp.Domain.Results.ResultStatus;

namespace MedApp.Application.Results.Queries.SearchResults;

/// <summary>The results list's views (<c>docs/centre-phases-3-5-plan.md</c>, decision 94), and every result.</summary>
public enum ResultView
{
    /// <summary>Every result, newest reported first (a pathway's or patient's results).</summary>
    All,

    /// <summary>Awaiting the viewer's sign-off, oldest reported first.</summary>
    AwaitingMine,

    /// <summary>Awaiting sign-off, everyone's, oldest reported first.</summary>
    Awaiting,

    /// <summary>Awaiting sign-off, whose responsible clinician can no longer sign off, oldest reported first.</summary>
    NeedsClinician,

    /// <summary>Signed off, newest signed off first.</summary>
    SignedOff
}

/// <summary>
/// A page of results: one view, optionally narrowed to a team, a pathway, a patient, or a patient's name
/// (<see cref="Search"/>). <see cref="Sort"/> names a column (<see cref="ResultSearch.SortColumns"/>); without one, the
/// view's own order.
/// </summary>
public sealed record SearchResultsQuery(
    int Page,
    int PageSize,
    string? View,
    Guid? TeamId,
    Guid? PathwayId,
    Guid? PatientId,
    Guid ViewerId,
    string? Search = null,
    string? Sort = null,
    bool Ascending = true)
    : IRequest<PagedResponse<ResultSummaryResponse>>
{
    public ResultView ParsedView => PathwayEnums.TryParse<ResultView>(View, out var view) ? view : ResultView.All;
}

/// <summary>The side nav's count of results awaiting the caller's sign-off. Records nothing (no patient is named).</summary>
public sealed record CountResultsQuery(Guid ViewerId) : IRequest<ResultCountResponse>;

public sealed class SearchResultsValidator : AbstractValidator<SearchResultsQuery>
{
    public SearchResultsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.View)
            .Must(view => view is null || PathwayEnums.TryParse<ResultView>(view, out _))
            .WithMessage($"View must be one of: {string.Join(", ", Enum.GetNames<ResultView>())}.");

        RuleFor(x => x.Search)
            .MaximumLength(ResultSearch.MaxSearchLength)
            .WithMessage($"Search must not exceed {ResultSearch.MaxSearchLength} characters.");
    }
}

/// <summary>
/// The views as LINQ that EF translates, so the same code runs in SQL and in unit tests. Ordered by dates only (decision
/// 97: nothing by content), ending on the ID so pages never repeat or skip one.
/// </summary>
public static class ResultSearch
{
    public const int MaxSearchLength = 200;

    public const string SortByPatient = "patient";
    public const string SortByTitle = "title";
    public const string SortByPathway = "pathway";
    public const string SortByReported = "reported";

    /// <summary>The status, then when it was signed off (the Signed off view's column).</summary>
    public const string SortByStatus = "status";

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByPatient, SortByTitle, SortByPathway, SortByReported, SortByStatus];

    public static IQueryable<Result> Filter(
        this IQueryable<Result> results, SearchResultsQuery query, IReadOnlyCollection<Guid> signOffHolders)
    {
        // The patient's name only: a result's title can hold a specimen number, which stays out of URLs.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            results = results.Where(r => (r.Patient.FirstName + " " + r.Patient.LastName).ToLower().Contains(term));
        }

        if (query.TeamId is { } teamId)
            results = results.Where(r => r.Pathway != null && r.Pathway.TeamId == teamId);
        if (query.PathwayId is { } pathwayId)
            results = results.Where(r => r.PathwayId == pathwayId);
        if (query.PatientId is { } patientId)
            results = results.Where(r => r.PatientId == patientId);

        var viewerId = query.ViewerId;
        return query.ParsedView switch
        {
            ResultView.AwaitingMine => results.Where(r =>
                (r.Status == AwaitingSignOff || r.Status == Seen) && r.ResponsibleUserId == viewerId),
            ResultView.Awaiting => results.Where(r => r.Status == AwaitingSignOff || r.Status == Seen),
            ResultView.NeedsClinician => results.Where(r =>
                (r.Status == AwaitingSignOff || r.Status == Seen) && !signOffHolders.Contains(r.ResponsibleUserId)),
            ResultView.SignedOff => results.Where(r => r.Status == SignedOff),
            _ => results
        };
    }

    /// <summary>
    /// The column named (<see cref="SortColumns"/>) in either direction, or without one the view's own order; either way
    /// ending on the ID. Columns the person chose, never anything a report says.
    /// </summary>
    public static IOrderedQueryable<Result> Sort(
        this IQueryable<Result> results, ResultView view, string? sort = null, bool ascending = true)
    {
        var ordered = Ordering.ColumnOf(sort, SortColumns) switch
        {
            SortByPatient => results.OrderByDirection(r => r.Patient.LastName, ascending)
                .ThenByDirection(r => r.Patient.FirstName, ascending),
            SortByTitle => results.OrderByDirection(r => r.Title, ascending),
            SortByPathway => results.OrderByDirection(r => r.Pathway != null ? r.Pathway.Title : null, ascending),
            SortByReported => results.OrderByDirection(r => r.ReportedAtUtc, ascending),
            SortByStatus => results.OrderByDirection(r => r.Status, ascending)
                .ThenByDirection(r => r.SignedOffAtUtc, ascending),
            _ => null
        };
        return ordered?.ThenBy(r => r.Id) ?? ViewOrder(results, view);
    }

    private static IOrderedQueryable<Result> ViewOrder(IQueryable<Result> results, ResultView view) => view switch
    {
        ResultView.AwaitingMine or ResultView.Awaiting or ResultView.NeedsClinician =>
            results.OrderBy(r => r.ReportedAtUtc).ThenBy(r => r.Id),
        ResultView.SignedOff => results.OrderByDescending(r => r.SignedOffAtUtc).ThenByDescending(r => r.Id),
        _ => results.OrderByDescending(r => r.ReportedAtUtc).ThenByDescending(r => r.Id),
    };
}

/// <summary>A page of results. Each patient on the page is recorded as listed (once per person every few minutes).</summary>
public sealed class SearchResultsHandler(
    IResultRepository repository,
    IPermissionHolders permissionHolders,
    IIdentityReadService identity,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<SearchResultsQuery, PagedResponse<ResultSummaryResponse>>,
      IRequestHandler<CountResultsQuery, ResultCountResponse>
{
    public async Task<PagedResponse<ResultSummaryResponse>> Handle(SearchResultsQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var holders = (await permissionHolders.GetUserIdsAsync(Permission.SignOffResults, cancellationToken)).ToHashSet();
        var result = await repository.SearchAsync(request, holders, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(r => r.PatientId).Distinct(), cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync(
            result.Items.SelectMany(r => new[] { r.ResponsibleUserId, r.SignedOffByUserId }).OfType<Guid>().Distinct(),
            cancellationToken);

        return PagedMapper.ToResponse(
            result, request.Page, request.PageSize, r => ResultMapper.ToSummary(r, usernames, holders, now));
    }

    public async Task<ResultCountResponse> Handle(CountResultsQuery request, CancellationToken cancellationToken) =>
        new() { AwaitingMine = await repository.CountAwaitingForAsync(request.ViewerId, cancellationToken) };
}
