using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Results.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Results;
using MedApp.Domain.Teams;
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
/// A page of results: one view, optionally narrowed to a team (the clinic picker: the result's own team, one of the
/// viewer's unless they see every team, else a 400), a pathway, a patient, or a patient's name (<see cref="Search"/>).
/// <see cref="Sort"/> names a column (<see cref="ResultSearch.SortColumns"/>); without one, the view's own order. Only
/// results the viewer's teams may see (<see cref="Scope"/>: a result follows its pathway, or its own team).
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
    bool Ascending = true,
    TeamScope? Scope = null)
    : IRequest<PagedResponse<ResultSummaryResponse>>
{
    /// <summary>The viewer's team walls (null only outside a request: the system's).</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;

    public ResultView ParsedView => PathwayEnums.TryParse<ResultView>(View, out var view) ? view : ResultView.All;
}

/// <summary>
/// The side nav's count of results awaiting the caller's sign-off, of those their teams may see. Records nothing (no
/// patient is named).
/// </summary>
public sealed record CountResultsQuery(Guid ViewerId, TeamScope? Scope = null) : IRequest<ResultCountResponse>
{
    /// <summary>The viewer's team walls (null only outside a request: the system's).</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}

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

    /// <summary>
    /// The view's results the viewer may see: their teams' own, or on a pathway they see in full
    /// (<see cref="TeamAccess.ByPathway{T}"/>; <paramref name="grants"/> is the grants table, needed unless the viewer sees
    /// every team).
    /// </summary>
    public static IQueryable<Result> Filter(
        this IQueryable<Result> results,
        SearchResultsQuery query,
        IReadOnlyCollection<Guid> signOffHolders,
        IQueryable<PathwayAccess>? grants = null)
    {
        results = results.Visible(query.Teams, grants);

        // The patient's name only: a result's title can hold a specimen number, which stays out of URLs.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            results = results.Where(r => (r.Patient.FirstName + " " + r.Patient.LastName).ToLower().Contains(term));
        }

        // The clinic picker: the result's own team (its pathway's, copied when it was made).
        if (query.TeamId is { } teamId)
            results = results.Where(r => r.TeamId == teamId);
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

    /// <summary>Only results the viewer may see (see <see cref="Filter"/>).</summary>
    public static IQueryable<Result> Visible(
        this IQueryable<Result> results, TeamScope scope, IQueryable<PathwayAccess>? grants = null) =>
        scope.SeesAllTeams
            ? results
            : results.Where(TeamAccess.ByPathway<Result>(
                scope, grants ?? throw new ArgumentNullException(nameof(grants)), r => r.PathwayId, r => r.TeamId));

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

/// <summary>
/// A page of results, of those the viewer's teams may see. Each patient on the page is recorded as listed (once per
/// person every few minutes; an admin's with <c>access</c> and the team picked, <see cref="TeamPicker.ListDetails"/>).
/// </summary>
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
        TeamPicker.EnsureCanPick(request.TeamId, request.Teams);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var holders = (await permissionHolders.GetUserIdsAsync(Permission.SignOffResults, cancellationToken)).ToHashSet();
        var result = await repository.SearchAsync(request, holders, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(r => r.PatientId).Distinct(), cancellationToken,
            details: TeamPicker.ListDetails(request.TeamId, request.Teams));

        var usernames = await identity.GetUsernamesByIdAsync(
            result.Items.SelectMany(r => new[] { r.ResponsibleUserId, r.SignedOffByUserId }).OfType<Guid>().Distinct(),
            cancellationToken);

        return PagedMapper.ToResponse(
            result, request.Page, request.PageSize, r => ResultMapper.ToSummary(r, usernames, holders, now));
    }

    public async Task<ResultCountResponse> Handle(CountResultsQuery request, CancellationToken cancellationToken) =>
        new() { AwaitingMine = await repository.CountAwaitingForAsync(request.ViewerId, request.Teams, cancellationToken) };
}
