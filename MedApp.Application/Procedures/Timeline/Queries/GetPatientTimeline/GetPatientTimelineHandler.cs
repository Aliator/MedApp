using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
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
    IAppointmentRepository appointmentRepository,
    IActivityLog activityLog)
    : IRequestHandler<GetPatientTimelineQuery, PatientTimelinePageResponse>
{
    public async Task<PatientTimelinePageResponse> Handle(GetPatientTimelineQuery request, CancellationToken cancellationToken)
    {
        _ = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
            ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [request.PatientId], cancellationToken);

        // The audit log has every kind but Appointments, and appointments' history only that one: ask each only when
        // the kinds filter wants something from it.
        var kinds = PatientTimelineKinds.Parse(request.Kinds);
        var wantsAudit = kinds.Count == 0 || kinds.Any(k => k != PatientTimelineKind.Appointments);
        var wantsAppointments = kinds.Count == 0 || kinds.Contains(PatientTimelineKind.Appointments);

        // Each side gives one more than a page, to tell whether there's another.
        IReadOnlyList<PatientTimelineRow> auditRows = wantsAudit
            ? await auditRepository.GetPatientTimelineAsync(request, cancellationToken)
            : [];
        IReadOnlyList<AppointmentTimelineRow> appointmentRows = wantsAppointments
            ? await appointmentRepository.GetPatientTimelineAsync(request, cancellationToken)
            : [];

        // Merged newest first. The sort is stable, so entries that share a time keep the order their own search gave
        // them (by ID, as the database orders IDs, which the next page's cursor compares the same way); the two sides
        // never share a time in practice, as each change's entries are a tick apart. Together they hold more than a
        // page whenever either side has more.
        var merged = auditRows
            .Select(row => new Entry(new TimelineCursor(row.Entry.AtUtc, row.Entry.Id), row, null))
            .Concat(appointmentRows.Select(row => new Entry(new TimelineCursor(row.Event.AtUtc, row.Event.Id), null, row)))
            .OrderByDescending(e => e.Position.AtUtc)
            .ToList();
        var page = merged.Take(request.PageSize).ToList();

        return new PatientTimelinePageResponse
        {
            // The searches already leave deleted procedures out for staff; this makes sure nothing slips through.
            Items = page
                .Where(e => request.IsAdmin
                            || (e.Audit is { } audit ? audit.CurrentProcedureTitle is not null : e.Appointment!.IsVisibleToStaff))
                .Select(e => e.Audit is { } audit
                    ? TimelineMapper.ToResponse(audit, request.IsAdmin)
                    : TimelineMapper.ToResponse(e.Appointment!, request.IsAdmin))
                .ToList(),
            NextCursor = merged.Count > request.PageSize ? page[^1].Position.Encode() : null,
        };
    }

    /// <summary>One entry of the merged feed, from the audit log or from appointments' history.</summary>
    private sealed record Entry(TimelineCursor Position, PatientTimelineRow? Audit, AppointmentTimelineRow? Appointment);
}
