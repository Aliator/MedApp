using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;

/// <summary>A page of a patient's timeline. Opening it is recorded as viewing the patient (once per person every few minutes).</summary>
public sealed class GetPatientTimelineHandler(
    IPatientRepository patientRepository,
    IProcedureAuditRepository auditRepository,
    IActivityLog activityLog)
    : IRequestHandler<GetPatientTimelineQuery, PatientTimelinePageResponse>
{
    public async Task<PatientTimelinePageResponse> Handle(GetPatientTimelineQuery request, CancellationToken cancellationToken)
    {
        _ = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
            ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [request.PatientId], cancellationToken);

        // One more than a page, to tell whether there's another.
        var rows = await auditRepository.GetPatientTimelineAsync(request, cancellationToken);
        var page = rows.Take(request.PageSize).ToList();

        return new PatientTimelinePageResponse
        {
            // The search already leaves deleted procedures out for staff; this makes sure nothing slips through.
            Items = page
                .Where(row => request.IsAdmin || row.CurrentProcedureTitle is not null)
                .Select(row => TimelineMapper.ToResponse(row, request.IsAdmin))
                .ToList(),
            NextCursor = rows.Count > request.PageSize ? TimelineCursor.After(page[^1].Entry).Encode() : null,
        };
    }
}
