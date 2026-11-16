using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Procedures.Timeline.Queries.GetPatientTimelineOverview;

/// <summary>What's due for a patient, and their procedures. Opening it is recorded as viewing the patient (once per person every few minutes).</summary>
public sealed class GetPatientTimelineOverviewHandler(
    IPatientRepository patientRepository,
    IPatientProcedureRepository procedureRepository,
    IProcedureAuditRepository auditRepository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetPatientTimelineOverviewQuery, PatientTimelineOverviewResponse>
{
    /// <summary>How many due procedures the overview lists; the rest are counted.</summary>
    public const int UpcomingLimit = 5;

    public async Task<PatientTimelineOverviewResponse> Handle(GetPatientTimelineOverviewQuery request, CancellationToken cancellationToken)
    {
        _ = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
            ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [request.PatientId], cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var procedures = await procedureRepository.GetByPatientAsync(request.PatientId, cancellationToken);

        // Open or blocked: still to be done, so still due.
        var due = procedures
            .Where(p => !ProcedureWorkflow.IsClosed(p.Status))
            .OrderBy(p => p.DueDateUtc)
            .ThenBy(p => p.Id)
            .ToList();

        var listed = procedures
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(TimelineMapper.ToProcedureResponse)
            .ToList();

        if (request.IsAdmin)
        {
            var existing = procedures.Select(p => p.Id).ToHashSet();
            var deletions = await auditRepository.GetDeletionsAsync(request.PatientId, cancellationToken);
            listed.AddRange(deletions
                .Where(d => !existing.Contains(d.PatientProcedureId))
                .OrderByDescending(d => d.AtUtc)
                .ThenBy(d => d.Id)
                .DistinctBy(d => d.PatientProcedureId)
                .Select(TimelineMapper.ToDeletedProcedureResponse));
        }

        return new PatientTimelineOverviewResponse
        {
            Upcoming = due.Take(UpcomingLimit).Select(p => TimelineMapper.ToUpcomingResponse(p, now)).ToList(),
            OpenCount = due.Count,
            Procedures = listed,
        };
    }
}
