using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;

/// <summary>What's due for a patient, and their pathways. Opening it is recorded as viewing the patient (once per person every few minutes).</summary>
public sealed class GetPatientTimelineOverviewHandler(
    IPatientRepository patientRepository,
    IPatientPathwayRepository pathwayRepository,
    IPathwayAuditRepository auditRepository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetPatientTimelineOverviewQuery, PatientTimelineOverviewResponse>
{
    /// <summary>How many due pathways the overview lists; the rest are counted.</summary>
    public const int UpcomingLimit = 5;

    public async Task<PatientTimelineOverviewResponse> Handle(GetPatientTimelineOverviewQuery request, CancellationToken cancellationToken)
    {
        _ = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
            ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [request.PatientId], cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var pathways = await pathwayRepository.GetByPatientAsync(request.PatientId, cancellationToken);

        // Open or blocked: still to be done, so still due.
        var due = pathways
            .Where(p => !PathwayWorkflow.IsClosed(p.Status))
            .OrderBy(p => p.DueDateUtc)
            .ThenBy(p => p.Id)
            .ToList();

        var listed = pathways
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(TimelineMapper.ToPathwayResponse)
            .ToList();

        if (request.FullView)
        {
            var existing = pathways.Select(p => p.Id).ToHashSet();
            var deletions = await auditRepository.GetDeletionsAsync(request.PatientId, cancellationToken);
            listed.AddRange(deletions
                .Where(d => !existing.Contains(d.PatientPathwayId))
                .OrderByDescending(d => d.AtUtc)
                .ThenBy(d => d.Id)
                .DistinctBy(d => d.PatientPathwayId)
                .Select(TimelineMapper.ToDeletedPathwayResponse));
        }

        return new PatientTimelineOverviewResponse
        {
            Upcoming = due.Take(UpcomingLimit).Select(p => TimelineMapper.ToUpcomingResponse(p, now)).ToList(),
            OpenCount = due.Count,
            Pathways = listed,
        };
    }
}
