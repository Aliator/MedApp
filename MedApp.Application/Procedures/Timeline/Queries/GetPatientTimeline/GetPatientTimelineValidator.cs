using FluentValidation;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Timeline.Common;

namespace MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;

public sealed class GetPatientTimelineValidator : AbstractValidator<GetPatientTimelineQuery>
{
    /// <summary>The most procedures the timeline can be filtered by at once.</summary>
    public const int MaxProcedureIds = 50;

    public GetPatientTimelineValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Before)
            .Must(before => TimelineCursor.TryDecode(before, out _))
            .When(x => x.Before is not null)
            .WithMessage("Before must be the NextCursor of an earlier page.");

        RuleForEach(x => x.Kinds)
            .Must(PatientTimelineKinds.IsKind)
            .WithMessage($"Each kind must be one of: {PatientTimelineKinds.Names}.");

        RuleFor(x => x.ProcedureIds)
            .Must(ids => ids!.Count <= MaxProcedureIds)
            .When(x => x.ProcedureIds is not null)
            .WithMessage($"Filter by at most {MaxProcedureIds} procedures.");
    }
}
