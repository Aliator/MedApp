using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Patients.Queries.SearchRetentionReview;

/// <summary>
/// The retention review list. The database narrows it to patients whose last change is at least the shortest period
/// old (<see cref="RetentionSchedule.ShortestYears"/>); the schedule's rules, which depend on each patient's age, are
/// then applied here, and the list sorted and paged in memory. That set is small: records only reach it years after
/// their care ends. The patients on the page are recorded in the activity log as seen, as a patient search's are.
/// </summary>
public sealed class SearchRetentionReviewHandler(
    IPatientRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider,
    IOptions<RetentionOptions> options)
    : IRequestHandler<SearchRetentionReviewQuery, PagedResponse<RetentionReviewItemResponse>>
{
    public async Task<PagedResponse<RetentionReviewItemResponse>> Handle(
        SearchRetentionReviewQuery request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DueDates.ToUkDate(now);

        // Anyone changed after the start of this day, that many years ago, can't be due yet.
        var changedBefore = DueDates.EndOfDayUtc(today.AddYears(-RetentionSchedule.ShortestYears(settings)));
        var candidates = await repository.GetRetentionCandidatesAsync(changedBefore, cancellationToken);

        var due = candidates
            .Select(c => (Candidate: c, Due: RetentionSchedule.Due(c, settings)))
            .Where(x => x.Due.DueOn <= today)
            .Where(x => request.IncludeKept || x.Candidate.KeptUntil is not { } until || until <= today)
            .OrderBy(x => x.Due.DueOn)
            .ThenBy(x => x.Candidate.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Candidate.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Candidate.PatientId)
            .ToList();

        var page = due
            .Skip(Paging.Skip(request.Page, request.PageSize))
            .Take(request.PageSize)
            .Select(x => RetentionReviewMapper.ToResponse(x.Candidate, x.Due))
            .ToList();

        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, page.Select(p => p.PatientId), cancellationToken);

        return PagedMapper.ToResponse(new PagedResult<RetentionReviewItemResponse>(page, due.Count), request.Page, request.PageSize);
    }
}
