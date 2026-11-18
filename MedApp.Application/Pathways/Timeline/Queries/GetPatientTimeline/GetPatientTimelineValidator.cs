using FluentValidation;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Timeline.Common;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;

public sealed class GetPatientTimelineValidator : AbstractValidator<GetPatientTimelineQuery>
{
    /// <summary>The most pathways the timeline can be filtered by at once.</summary>
    public const int MaxPathwayIds = 50;

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

        RuleFor(x => x.PathwayIds)
            .Must(ids => ids!.Count <= MaxPathwayIds)
            .When(x => x.PathwayIds is not null)
            .WithMessage($"Filter by at most {MaxPathwayIds} pathways.");
    }
}
