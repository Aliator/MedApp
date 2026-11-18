using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;

/// <summary>A page of a patient's timeline. Opening it is recorded as viewing the patient (once per person every few minutes).</summary>
public sealed class GetPatientTimelineHandler(
    IPatientRepository patientRepository,
    IPathwayAuditRepository auditRepository,
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

        // Admins see the text of the notes added and edited, from the note versions (the audit holds none).
        if (request.IsAdmin)
            page = await WithNoteTextAsync(request.PatientId, page, cancellationToken);

        return new PatientTimelinePageResponse
        {
            // The searches already leave deleted pathways out for staff; this makes sure nothing slips through.
            Items = page
                .Where(e => request.IsAdmin
                            || (e.Audit is { } audit
                                ? audit.CurrentPathwayTitle is not null || audit.IsAboutPatient
                                : e.Appointment!.IsVisibleToStaff))
                .Select(e => e.Audit is { } audit
                    ? TimelineMapper.ToResponse(audit, request.IsAdmin)
                    : TimelineMapper.ToResponse(e.Appointment!, request.IsAdmin))
                .ToList(),
            NextCursor = merged.Count > request.PageSize ? page[^1].Position.Encode() : null,
        };
    }

    /// <summary>The page with each note entry's text, and for an edit the text before, from the note versions.</summary>
    private async Task<List<Entry>> WithNoteTextAsync(Guid patientId, List<Entry> page, CancellationToken ct)
    {
        var keys = page
            .Select(e => e.Audit is { } audit ? PathwayNotes.VersionOf(audit.Entry) : null)
            .OfType<NoteVersionKey>()
            .ToList();
        if (keys.Count == 0)
            return page;

        var versions = (await auditRepository.GetNoteVersionsAsync(patientId, keys.Distinct().ToList(), ct))
            .ToDictionary(v => new NoteVersionKey(v.NoteId, v.Version), v => v.Text);

        return page
            .Select(e => e.Audit is { } audit && PathwayNotes.VersionOf(audit.Entry) is { } key
                ? e with
                {
                    Audit = audit with
                    {
                        NoteText = versions.GetValueOrDefault(key),
                        PreviousNoteText = key.Version > 1 ? versions.GetValueOrDefault(key with { Version = key.Version - 1 }) : null
                    }
                }
                : e)
            .ToList();
    }

    /// <summary>One entry of the merged feed, from the audit log or from appointments' history.</summary>
    private sealed record Entry(TimelineCursor Position, PatientTimelineRow? Audit, AppointmentTimelineRow? Appointment);
}
