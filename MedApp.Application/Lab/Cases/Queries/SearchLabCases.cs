using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Common;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Lab;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Lab.Cases.Queries;

/// <summary>The lab cases page's tabs and the lab home's lists (MC15, MC17, MC19, MC20).</summary>
public enum LabCaseView
{
    /// <summary>Open cases, soonest due first (the default).</summary>
    Open,

    /// <summary>Open cases whose clock is at risk.</summary>
    AtRisk,

    /// <summary>Open cases past their due date.</summary>
    Overdue,

    /// <summary>Open cases with every step done: their results to enter and authorise.</summary>
    ToReport,

    /// <summary>Authorised, whatever came after, newest first.</summary>
    Reported,

    /// <summary>Hospital cases whose report went back, waiting longest first (MC20).</summary>
    AwaitingSurgeryDetails,

    /// <summary>Closed with a repeat sample asked for, newest first.</summary>
    RepeatRequested,

    /// <summary>Every case, newest opened first.</summary>
    All
}

/// <summary>
/// A page of the lab cases the viewer sees (the lab and admins all; a clinic its own clinic's), in a view, narrowed by
/// source, test and a search (the number, the patient's name or NHS number, the test, where it came from; in the body,
/// as it may be a case number), sorted by a column or the view's order.
/// </summary>
public sealed record SearchLabCasesQuery(
    int Page,
    int PageSize,
    string? View,
    string? Source,
    Guid? LabTestId,
    string? Search,
    string? Sort,
    bool Ascending,
    PathwayActor Viewer)
    : IRequest<PagedResponse<LabCaseSummaryResponse>>
{
    public LabCaseView ParsedView => PathwayEnums.TryParse<LabCaseView>(View, out var view) ? view : LabCaseView.Open;

    /// <summary>Whether it's worked out in memory: the views and the column that follow a case's clock or next step.</summary>
    public bool NeedsMemory =>
        ParsedView is LabCaseView.Open or LabCaseView.AtRisk or LabCaseView.Overdue or LabCaseView.ToReport
        || Ordering.ColumnOf(Sort, LabCaseSearch.SortColumns) == LabCaseSearch.SortByDue;
}

/// <summary>
/// The home page's counts of the open lab cases the caller's teams see (the lab and admins all; a clinic its own
/// clinic's): at risk, overdue, and ready to report, as the views count them. Records nothing (no patient is named).
/// </summary>
public sealed record CountLabCasesQuery(TeamScope? Scope = null) : IRequest<LabCaseCountResponse>
{
    /// <summary>The viewer's team walls (null only outside a request: the system's).</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}

public sealed class SearchLabCasesValidator : AbstractValidator<SearchLabCasesQuery>
{
    public SearchLabCasesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or more.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");
        RuleFor(x => x.View)
            .Must(view => string.IsNullOrWhiteSpace(view) || PathwayEnums.TryParse<LabCaseView>(view, out _))
            .WithMessage($"View must be one of: {string.Join(", ", Enum.GetNames<LabCaseView>())}.");
        RuleFor(x => x.Source)
            .Must(source => string.IsNullOrWhiteSpace(source) || PathwayEnums.TryParse<SpecimenSource>(source, out _))
            .WithMessage($"Source must be one of: {string.Join(", ", Enum.GetNames<SpecimenSource>())}.");
        RuleFor(x => x.Search)
            .MaximumLength(LabCaseSearch.MaxSearchLength)
            .WithMessage($"Search must not exceed {LabCaseSearch.MaxSearchLength} characters.");
    }
}

/// <summary>
/// The views (the light demo's <c>VIEWS</c>): narrowed in SQL by status, source, test and text; the open views and the
/// due column follow each case's clock and next step, so they're ordered in memory (open cases only), the rest in SQL.
/// Every order ends on the ID.
/// </summary>
public static class LabCaseSearch
{
    public const int MaxSearchLength = 200;

    public const string SortByNumber = "number";
    public const string SortByPatient = "patient";
    public const string SortBySource = "source";
    public const string SortByTest = "test";
    public const string SortByOpened = "opened";
    public const string SortByDue = "due";

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByNumber, SortByPatient, SortBySource, SortByTest, SortByOpened, SortByDue];

    public static IQueryable<LabCase> Filter(this IQueryable<LabCase> cases, SearchLabCasesQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            var compact = term.Replace(" ", "");
            cases = cases.Where(c =>
                c.Number.ToLower().Contains(compact)
                || (c.Patient.FirstName + " " + c.Patient.LastName).ToLower().Contains(term)
                || (c.Patient.NhsNumber != null && c.Patient.NhsNumber.Contains(compact))
                || (c.LabTest != null && c.LabTest.Name.ToLower().Contains(term))
                || (c.RequestingTeam != null && c.RequestingTeam.Name.ToLower().Contains(term))
                || (c.Hospital != null && c.Hospital.Name.ToLower().Contains(term)));
        }

        if (PathwayEnums.TryParse<SpecimenSource>(query.Source, out var source))
            cases = cases.Where(c => c.Source == source);
        if (query.LabTestId is { } labTestId)
            cases = cases.Where(c => c.LabTestId == labTestId);

        return query.ParsedView switch
        {
            LabCaseView.Open or LabCaseView.AtRisk or LabCaseView.Overdue or LabCaseView.ToReport =>
                cases.Where(c => c.Status == LabCaseStatus.Open),
            LabCaseView.Reported => cases.Where(c => c.ReportedAtUtc != null),
            LabCaseView.AwaitingSurgeryDetails => cases.Where(c => c.Status == LabCaseStatus.AwaitingSurgeryDetails),
            LabCaseView.RepeatRequested => cases.Where(c => c.Status == LabCaseStatus.RepeatRequested),
            _ => cases
        };
    }

    /// <summary>The view's own order in SQL (for the views not ordered by the clock), or the column asked for.</summary>
    public static IOrderedQueryable<LabCase> Sort(this IQueryable<LabCase> cases, LabCaseView view, string? sort, bool ascending)
    {
        var ordered = Ordering.ColumnOf(sort, SortColumns) switch
        {
            SortByNumber => cases.OrderByDirection(c => c.Number, ascending),
            SortByPatient => cases.OrderByDirection(c => c.Patient.LastName, ascending)
                .ThenByDirection(c => c.Patient.FirstName, ascending),
            SortBySource => cases.OrderByDirection(
                c => c.RequestingTeam != null ? c.RequestingTeam.Name : c.Hospital != null ? c.Hospital.Name : "", ascending),
            SortByTest => cases.OrderByDirection(c => c.LabTest != null ? c.LabTest.Name : "", ascending),
            SortByOpened => cases.OrderByDirection(c => c.OpenedAtUtc, ascending),
            _ => null
        };
        if (ordered is not null)
            return ordered.ThenBy(c => c.Id);

        return view switch
        {
            LabCaseView.Reported => cases.OrderByDescending(c => c.ReportedAtUtc).ThenByDescending(c => c.Id),
            LabCaseView.AwaitingSurgeryDetails => cases.OrderBy(c => c.ReportSentAtUtc).ThenBy(c => c.Id),
            LabCaseView.RepeatRequested => cases.OrderByDescending(c => c.RepeatRequestedAtUtc).ThenByDescending(c => c.Id),
            _ => cases.OrderByDescending(c => c.OpenedAtUtc).ThenByDescending(c => c.Id),
        };
    }

    /// <summary>A case's due moment for ordering: none sorts last.</summary>
    public static DateTime DueKey(LabCase labCase, DateTime nowUtc) => LabClock.Of(labCase, nowUtc)?.DueAtUtc ?? DateTime.MaxValue;

    /// <summary>A running clock's state, for the at-risk and overdue views: open cases only.</summary>
    private static string? ClockState(LabCase labCase, DateTime nowUtc) =>
        labCase.Status == LabCaseStatus.Open ? LabClock.Of(labCase, nowUtc)?.State : null;

    /// <summary>
    /// Whether a case SQL narrowed to the view is in it, by what's worked out in memory: the clock for at risk and
    /// overdue, the next step for to report (open cases only); any other view's are all in it.
    /// </summary>
    public static bool IsInView(LabCase labCase, LabCaseView view, DateTime nowUtc) => view switch
    {
        LabCaseView.AtRisk => ClockState(labCase, nowUtc) == LabClock.AtRisk,
        LabCaseView.Overdue => ClockState(labCase, nowUtc) == LabClock.Breached,
        LabCaseView.ToReport => LabCaseRules.Next(labCase)?.Kind == LabNextKind.Report,
        _ => true
    };

    /// <summary>The home page's counts over the open cases the viewer sees, as the views count them.</summary>
    public static LabCaseCountResponse Count(IReadOnlyCollection<LabCase> open, DateTime nowUtc)
    {
        var cases = open.Where(c => c.Status == LabCaseStatus.Open).ToList();
        return new LabCaseCountResponse
        {
            AtRisk = cases.Count(c => IsInView(c, LabCaseView.AtRisk, nowUtc)),
            Overdue = cases.Count(c => IsInView(c, LabCaseView.Overdue, nowUtc)),
            ToReport = cases.Count(c => IsInView(c, LabCaseView.ToReport, nowUtc)),
        };
    }

    /// <summary>The views and column that follow the clock and next step, in memory, over the cases SQL narrowed.</summary>
    public static IReadOnlyList<LabCase> Arrange(IEnumerable<LabCase> narrowed, SearchLabCasesQuery query, DateTime nowUtc)
    {
        var view = query.ParsedView;
        var items = narrowed.Where(c => IsInView(c, view, nowUtc));

        IOrderedEnumerable<LabCase> ordered = view switch
        {
            LabCaseView.Open or LabCaseView.AtRisk or LabCaseView.Overdue or LabCaseView.ToReport =>
                items.OrderBy(c => DueKey(c, nowUtc)).ThenBy(c => c.Id),
            LabCaseView.Reported => items.OrderByDescending(c => c.ReportedAtUtc).ThenByDescending(c => c.Id),
            LabCaseView.AwaitingSurgeryDetails => items.OrderBy(c => c.ReportSentAtUtc).ThenBy(c => c.Id),
            LabCaseView.RepeatRequested => items.OrderByDescending(c => c.RepeatRequestedAtUtc).ThenByDescending(c => c.Id),
            _ => items.OrderByDescending(c => c.OpenedAtUtc).ThenByDescending(c => c.Id),
        };

        var ascending = query.Ascending;
        IEnumerable<LabCase> result = Ordering.ColumnOf(query.Sort, SortColumns) switch
        {
            SortByNumber => By(ordered, c => c.Number, ascending),
            SortByPatient => By(ordered, c => (c.Patient?.LastName ?? "") + "\u0000" + (c.Patient?.FirstName ?? ""), ascending),
            SortBySource => By(ordered, c => c.RequestingTeam?.Name ?? c.Hospital?.Name ?? "", ascending),
            SortByTest => By(ordered, c => c.LabTest?.Name ?? "", ascending),
            SortByOpened => By(ordered, c => c.OpenedAtUtc, ascending),
            SortByDue => By(ordered, c => DueKey(c, nowUtc), ascending),
            _ => ordered
        };
        return result.ToList();
    }

    private static IEnumerable<LabCase> By<TKey>(IEnumerable<LabCase> items, Func<LabCase, TKey> key, bool ascending) =>
        ascending ? items.OrderBy(key).ThenBy(c => c.Id) : items.OrderByDescending(key).ThenBy(c => c.Id);
}

/// <summary>A page of lab cases. Each patient on the page is recorded as listed.</summary>
public sealed class SearchLabCasesHandler(
    ILabCaseRepository repository,
    ITeamRepository teams,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<SearchLabCasesQuery, PagedResponse<LabCaseSummaryResponse>>
{
    public async Task<PagedResponse<LabCaseSummaryResponse>> Handle(SearchLabCasesQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var labTeamId = (await teams.GetLabAsync(cancellationToken))?.Id;
        var result = await repository.SearchAsync(request, request.Viewer.Teams, labTeamId, now, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(c => c.PatientId).Distinct(), cancellationToken);

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, c => LabCaseMapper.ToSummary(c, now));
    }
}

/// <summary>
/// The home page's counts of the open lab cases the viewer's teams see. Records nothing: counts name no patient.
/// </summary>
public sealed class CountLabCasesHandler(
    ILabCaseRepository repository,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<CountLabCasesQuery, LabCaseCountResponse>
{
    public async Task<LabCaseCountResponse> Handle(CountLabCasesQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var labTeamId = (await teams.GetLabAsync(cancellationToken))?.Id;
        var open = await repository.GetOpenAsync(request.Teams, labTeamId, cancellationToken);
        return LabCaseSearch.Count(open, now);
    }
}
