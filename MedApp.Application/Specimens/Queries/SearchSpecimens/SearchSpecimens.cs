using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Specimens;
using MediatR;
using static MedApp.Domain.Specimens.SpecimenStatus;

namespace MedApp.Application.Specimens.Queries.SearchSpecimens;

/// <summary>The lab's worklist views (<c>docs/centre-phases-3-5-plan.md</c>, decision 90), and every specimen.</summary>
public enum SpecimenView
{
    /// <summary>Every specimen, newest taken first (a pathway's or patient's specimens).</summary>
    All,

    /// <summary>Taken, not yet sent: waiting to go to the lab.</summary>
    ToSend,

    /// <summary>Sent, not yet received: on their way to the lab.</summary>
    Expected,

    /// <summary>Received, not yet reported.</summary>
    InLab,

    /// <summary>Open past the type's expected days.</summary>
    Overdue,

    /// <summary>Rejected by the lab or lost in the last 30 days.</summary>
    RejectedAndLost,

    /// <summary>Reported in the last 7 days.</summary>
    Reported
}

/// <summary>A page of specimens: one view, optionally narrowed to a lab, a pathway or a patient.</summary>
public sealed record SearchSpecimensQuery(
    int Page,
    int PageSize,
    string? View,
    Guid? LaboratoryId,
    Guid? PathwayId,
    Guid? PatientId)
    : IRequest<PagedResponse<SpecimenSummaryResponse>>
{
    public SpecimenView ParsedView =>
        PathwayEnums.TryParse<SpecimenView>(View, out var view) ? view : SpecimenView.All;
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
    }
}

/// <summary>
/// The views as LINQ that EF translates, so the same code runs in SQL and in unit tests. The worklist views are oldest
/// first (by the time that put each specimen in the view), ending on the ID so pages never repeat or skip one.
/// </summary>
public static class SpecimenSearch
{
    public static IQueryable<Specimen> Filter(this IQueryable<Specimen> specimens, SearchSpecimensQuery query, DateTime nowUtc)
    {
        if (query.LaboratoryId is { } laboratoryId)
            specimens = specimens.Where(s => s.LaboratoryId == laboratoryId);
        if (query.PathwayId is { } pathwayId)
            specimens = specimens.Where(s => s.PathwayId == pathwayId);
        if (query.PatientId is { } patientId)
            specimens = specimens.Where(s => s.PatientId == patientId);

        var rejectedSince = nowUtc - SpecimenWorkflow.RejectedAndLostWindow;
        var reportedSince = nowUtc - SpecimenWorkflow.ReportedWindow;

        return query.ParsedView switch
        {
            SpecimenView.ToSend => specimens.Where(s => s.Status == Taken),
            SpecimenView.Expected => specimens.Where(s => s.Status == Sent),
            SpecimenView.InLab => specimens.Where(s => s.Status == Received),
            SpecimenView.Overdue => specimens.Where(s =>
                (s.Status == Taken || s.Status == Sent || s.Status == Received)
                && s.TakenAtUtc.AddDays(s.SpecimenType.ExpectedTurnaroundDays) < nowUtc),
            SpecimenView.RejectedAndLost => specimens.Where(s =>
                (s.Status == RejectedByLab || s.Status == Lost) && s.ClosedAtUtc >= rejectedSince),
            SpecimenView.Reported => specimens.Where(s => s.Status == Reported && s.ReportedAtUtc >= reportedSince),
            _ => specimens
        };
    }

    public static IOrderedQueryable<Specimen> Sort(this IQueryable<Specimen> specimens, SpecimenView view) => view switch
    {
        SpecimenView.Expected => specimens.OrderBy(s => s.SentAtUtc).ThenBy(s => s.Id),
        SpecimenView.InLab => specimens.OrderBy(s => s.ReceivedAtUtc).ThenBy(s => s.Id),
        SpecimenView.RejectedAndLost => specimens.OrderBy(s => s.ClosedAtUtc).ThenBy(s => s.Id),
        SpecimenView.Reported => specimens.OrderBy(s => s.ReportedAtUtc).ThenBy(s => s.Id),
        SpecimenView.ToSend or SpecimenView.Overdue => specimens.OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Id),
        _ => specimens.OrderByDescending(s => s.TakenAtUtc).ThenByDescending(s => s.Id),
    };
}

/// <summary>A page of specimens. Each patient on the page is recorded as listed (once per person every few minutes).</summary>
public sealed class SearchSpecimensHandler(ISpecimenRepository repository, IActivityLog activityLog, TimeProvider timeProvider)
    : IRequestHandler<SearchSpecimensQuery, PagedResponse<SpecimenSummaryResponse>>
{
    public async Task<PagedResponse<SpecimenSummaryResponse>> Handle(SearchSpecimensQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = await repository.SearchAsync(request, now, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(s => s.PatientId).Distinct(), cancellationToken);

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, s => SpecimenMapper.ToSummary(s, now));
    }
}
