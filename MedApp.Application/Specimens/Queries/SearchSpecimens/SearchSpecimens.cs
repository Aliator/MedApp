using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Specimens;
using MediatR;
using static MedApp.Domain.Specimens.SpecimenStatus;

namespace MedApp.Application.Specimens.Queries.SearchSpecimens;

/// <summary>The samples page's tabs and the lab's worklist views (decision 90; MC14), and every sample.</summary>
public enum SpecimenView
{
    /// <summary>Every sample, newest taken first (a pathway's or patient's samples).</summary>
    All,

    /// <summary>Taken, not yet sent: waiting to go to the lab.</summary>
    ToSend,

    /// <summary>Sent, not yet received: on their way to the lab.</summary>
    Expected,

    /// <summary>Received, not yet reported.</summary>
    InLab,

    /// <summary>Open past when they were expected (their case's due date, MC19, else the type's expected days).</summary>
    Overdue,

    /// <summary>Rejected by the lab or lost in the last 30 days.</summary>
    RejectedAndLost,

    /// <summary>Reported in the last 7 days.</summary>
    Reported,

    /// <summary>MC14's first tab: not yet at the lab (taken, or on its way), oldest taken first.</summary>
    ToReceive
}

/// <summary>
/// A page of samples the viewer may see (MC14's walls: the lab and admins all, a clinic its own; <see cref="Scope"/>):
/// one view, optionally narrowed to a pathway or a patient, the samples page's filters (source, clinic, test, status,
/// due) and (sent in a request body) text in the number, the patient's name or NHS number, the type, the test or where
/// it came from. <see cref="Sort"/> names a column (<see cref="SpecimenSearch.SortColumns"/>); without one, the view's order.
/// </summary>
public sealed record SearchSpecimensQuery(
    int Page,
    int PageSize,
    string? View,
    Guid? PathwayId,
    Guid? PatientId,
    string? Search = null,
    string? Sort = null,
    bool Ascending = true,
    string? Source = null,
    Guid? TeamId = null,
    Guid? LabTestId = null,
    string? Status = null,
    string? Due = null,
    TeamScope? Scope = null)
    : IRequest<PagedResponse<SpecimenSummaryResponse>>
{
    public SpecimenView ParsedView =>
        PathwayEnums.TryParse<SpecimenView>(View, out var view) ? view : SpecimenView.All;

    public TeamScope Teams => Scope ?? TeamScope.System;

    /// <summary>Whether it's worked out in memory: the views and filters that follow a lab case's clock (MC19).</summary>
    public bool NeedsClock =>
        ParsedView == SpecimenView.Overdue
        || !string.IsNullOrWhiteSpace(Due)
        || Ordering.ColumnOf(Sort, SpecimenSearch.SortColumns) == SpecimenSearch.SortByDue;
}

public sealed class SearchSpecimensValidator : AbstractValidator<SearchSpecimensQuery>
{
    public SearchSpecimensValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.View)
            .Must(view => view is null || PathwayEnums.TryParse<SpecimenView>(view, out _))
            .WithMessage($"View must be one of: {string.Join(", ", Enum.GetNames<SpecimenView>())}.");

        RuleFor(x => x.Search)
            .MaximumLength(SpecimenSearch.MaxSearchLength)
            .WithMessage($"Search must not exceed {SpecimenSearch.MaxSearchLength} characters.");

        RuleFor(x => x.Source)
            .Must(source => string.IsNullOrWhiteSpace(source) || PathwayEnums.TryParse<SpecimenSource>(source, out _))
            .WithMessage($"Source must be one of: {string.Join(", ", Enum.GetNames<SpecimenSource>())}.");

        RuleFor(x => x.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) || PathwayEnums.TryParse<SpecimenStatus>(status, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<SpecimenStatus>())}.");

        RuleFor(x => x.Due)
            .Must(due => string.IsNullOrWhiteSpace(due)
                         || SpecimenWorkflow.DueValues.Contains(due.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Due must be one of: {string.Join(", ", SpecimenWorkflow.DueValues)}.");
    }
}

/// <summary>
/// The views as LINQ that EF translates, so the same code runs in SQL and in unit tests, and (for what follows a lab
/// case's clock) in memory. The worklist views are oldest first (by the time that put each sample in the view), ending
/// on the ID so pages never repeat or skip one.
/// </summary>
public static class SpecimenSearch
{
    public const int MaxSearchLength = 200;

    public const string SortByNumber = "number";
    public const string SortByPatient = "patient";
    public const string SortByStatus = "status";
    public const string SortBySource = "source";
    public const string SortByTest = "test";
    public const string SortByDue = "due";

    /// <summary>The view's own time: taken, sent, received, expected by, closed or reported.</summary>
    public const string SortByTime = "time";

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByNumber, SortByPatient, SortByStatus, SortBySource, SortByTest, SortByDue, SortByTime];

    /// <summary>
    /// Narrows in SQL: the text, the filters and the view, except what follows a lab case's clock (the Overdue view and
    /// the Due filter: open samples here, the rest in memory, <see cref="Arrange"/>).
    /// </summary>
    public static IQueryable<Specimen> Filter(this IQueryable<Specimen> specimens, SearchSpecimensQuery query, DateTime nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // A number is matched without its spaces (as typed or scanned); NHS numbers are stored as their digits.
            var term = query.Search.Trim().ToLower();
            var compact = term.Replace(" ", "");
            specimens = specimens.Where(s =>
                s.Number.ToLower().Contains(compact)
                || (s.Patient.FirstName + " " + s.Patient.LastName).ToLower().Contains(term)
                || (s.Patient.NhsNumber != null && s.Patient.NhsNumber.Contains(compact))
                || s.SpecimenType.Name.ToLower().Contains(term)
                || (s.LabTest != null && s.LabTest.Name.ToLower().Contains(term))
                || (s.RequestingTeam != null && s.RequestingTeam.Name.ToLower().Contains(term)));
        }

        if (query.PathwayId is { } pathwayId)
            specimens = specimens.Where(s => s.PathwayId == pathwayId);
        if (query.PatientId is { } patientId)
            specimens = specimens.Where(s => s.PatientId == patientId);
        if (PathwayEnums.TryParse<SpecimenSource>(query.Source, out var source))
            specimens = specimens.Where(s => s.Source == source);
        if (query.TeamId is { } teamId)
            specimens = specimens.Where(s => s.RequestingTeamId == teamId);
        if (query.LabTestId is { } labTestId)
            specimens = specimens.Where(s => s.LabTestId == labTestId);
        if (PathwayEnums.TryParse<SpecimenStatus>(query.Status, out var status))
            specimens = specimens.Where(s => s.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Due))
            specimens = specimens.Where(s => s.Status == Taken || s.Status == Sent || s.Status == Received);

        var rejectedSince = nowUtc - SpecimenWorkflow.RejectedAndLostWindow;
        var reportedSince = nowUtc - SpecimenWorkflow.ReportedWindow;

        return query.ParsedView switch
        {
            SpecimenView.ToReceive => specimens.Where(s => s.Status == Taken || s.Status == Sent),
            SpecimenView.ToSend => specimens.Where(s => s.Status == Taken),
            SpecimenView.Expected => specimens.Where(s => s.Status == Sent),
            SpecimenView.InLab => specimens.Where(s => s.Status == Received),
            SpecimenView.Overdue => specimens.Where(s => s.Status == Taken || s.Status == Sent || s.Status == Received),
            SpecimenView.RejectedAndLost => specimens.Where(s =>
                (s.Status == RejectedByLab || s.Status == Lost) && s.ClosedAtUtc >= rejectedSince),
            SpecimenView.Reported => specimens.Where(s => s.Status == Reported && s.ReportedAtUtc >= reportedSince),
            _ => specimens
        };
    }

    /// <summary>
    /// The column named (<see cref="SortColumns"/>) in either direction, or without one the view's own order; either
    /// way ending on the ID. The time column is the view's time. (Due is sorted in memory.)
    /// </summary>
    public static IOrderedQueryable<Specimen> Sort(
        this IQueryable<Specimen> specimens, SpecimenView view, string? sort = null, bool ascending = true)
    {
        var ordered = Ordering.ColumnOf(sort, SortColumns) switch
        {
            SortByNumber => specimens.OrderByDirection(s => s.Number, ascending),
            SortByPatient => specimens.OrderByDirection(s => s.Patient.LastName, ascending)
                .ThenByDirection(s => s.Patient.FirstName, ascending),
            SortByStatus => specimens.OrderByDirection(s => s.Status, ascending),
            SortBySource => specimens.OrderByDirection(s => s.RequestingTeam != null ? s.RequestingTeam.Name : "", ascending),
            SortByTest => specimens.OrderByDirection(s => s.LabTest != null ? s.LabTest.Name : "", ascending),
            SortByTime => view switch
            {
                SpecimenView.Expected => specimens.OrderByDirection(s => s.SentAtUtc, ascending),
                SpecimenView.InLab => specimens.OrderByDirection(s => s.ReceivedAtUtc, ascending),
                SpecimenView.RejectedAndLost => specimens.OrderByDirection(s => s.ClosedAtUtc, ascending),
                SpecimenView.Reported => specimens.OrderByDirection(s => s.ReportedAtUtc, ascending),
                _ => specimens.OrderByDirection(s => s.TakenAtUtc, ascending),
            },
            _ => null
        };
        return ordered?.ThenBy(s => s.Id) ?? ViewOrder(specimens, view);
    }

    private static IOrderedQueryable<Specimen> ViewOrder(IQueryable<Specimen> specimens, SpecimenView view) => view switch
    {
        SpecimenView.Expected => specimens.OrderBy(s => s.SentAtUtc).ThenBy(s => s.Id),
        SpecimenView.InLab => specimens.OrderBy(s => s.ReceivedAtUtc).ThenBy(s => s.Id),
        SpecimenView.RejectedAndLost => specimens.OrderBy(s => s.ClosedAtUtc).ThenBy(s => s.Id),
        SpecimenView.Reported => specimens.OrderBy(s => s.ReportedAtUtc).ThenBy(s => s.Id),
        SpecimenView.ToSend or SpecimenView.ToReceive or SpecimenView.Overdue => specimens.OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Id),
        _ => specimens.OrderByDescending(s => s.TakenAtUtc).ThenByDescending(s => s.Id),
    };

    /// <summary>
    /// What follows a lab case's clock, in memory (the light demo's <c>specimenList</c>): the Overdue view (open past when
    /// expected, soonest first), the Due filter, and sorting by due; then the column asked for. Ends on the ID.
    /// </summary>
    public static IReadOnlyList<Specimen> Arrange(IEnumerable<Specimen> narrowed, SearchSpecimensQuery query, DateTime nowUtc)
    {
        var view = query.ParsedView;
        var items = narrowed
            .Where(s => view != SpecimenView.Overdue || SpecimenWorkflow.IsOverdueAtLab(s, nowUtc))
            .Where(s => SpecimenWorkflow.MatchesDue(s, query.Due, nowUtc));

        IOrderedEnumerable<Specimen> ordered = view switch
        {
            SpecimenView.Overdue => items.OrderBy(s => SpecimenWorkflow.ExpectedBy(s, nowUtc)).ThenBy(s => s.Id),
            SpecimenView.Expected => items.OrderBy(s => s.SentAtUtc).ThenBy(s => s.Id),
            SpecimenView.InLab => items.OrderBy(s => s.ReceivedAtUtc).ThenBy(s => s.Id),
            SpecimenView.RejectedAndLost => items.OrderBy(s => s.ClosedAtUtc).ThenBy(s => s.Id),
            SpecimenView.Reported => items.OrderBy(s => s.ReportedAtUtc).ThenBy(s => s.Id),
            SpecimenView.ToSend or SpecimenView.ToReceive => items.OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Id),
            _ => items.OrderByDescending(s => s.TakenAtUtc).ThenByDescending(s => s.Id),
        };

        var ascending = query.Ascending;
        IEnumerable<Specimen> result = Ordering.ColumnOf(query.Sort, SortColumns) switch
        {
            SortByNumber => By(ordered, s => s.Number, ascending),
            SortByPatient => By(ordered, s => (s.Patient?.LastName ?? "") + "\u0000" + (s.Patient?.FirstName ?? ""), ascending),
            SortByStatus => By(ordered, s => (int)s.Status, ascending),
            SortBySource => By(ordered, s => s.RequestingTeam?.Name ?? "", ascending),
            SortByTest => By(ordered, s => s.LabTest?.Name ?? "", ascending),
            SortByDue => By(ordered, s => SpecimenWorkflow.ExpectedBy(s, nowUtc), ascending),
            SortByTime => view == SpecimenView.Overdue
                ? By(ordered, s => SpecimenWorkflow.ExpectedBy(s, nowUtc), ascending)
                : ordered,
            _ => ordered
        };
        return result.ToList();
    }

    private static IEnumerable<Specimen> By<TKey>(IEnumerable<Specimen> items, Func<Specimen, TKey> key, bool ascending) =>
        ascending
            ? items.OrderBy(key).ThenBy(s => s.Id)
            : items.OrderByDescending(key).ThenBy(s => s.Id);
}

/// <summary>A page of samples. Each patient on the page is recorded as listed (once per person every few minutes).</summary>
public sealed class SearchSpecimensHandler(
    ISpecimenRepository repository,
    ITeamRepository teams,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<SearchSpecimensQuery, PagedResponse<SpecimenSummaryResponse>>
{
    public async Task<PagedResponse<SpecimenSummaryResponse>> Handle(SearchSpecimensQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var labTeamId = (await teams.GetLabAsync(cancellationToken))?.Id;
        var result = await repository.SearchAsync(request, labTeamId, now, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(s => s.PatientId).Distinct(), cancellationToken);

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, s => SpecimenMapper.ToSummary(s, now));
    }
}
