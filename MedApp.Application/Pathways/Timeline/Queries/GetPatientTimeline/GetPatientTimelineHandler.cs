using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Referrals;
using MediatR;

namespace MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;

/// <summary>A page of a patient's timeline. Opening it is recorded as viewing the patient (once per person every few minutes).</summary>
public sealed class GetPatientTimelineHandler(
    IPatientRepository patientRepository,
    IPathwayAuditRepository auditRepository,
    IAppointmentRepository appointmentRepository,
    IReferralRepository referralRepository,
    ILetterRepository letterRepository,
    IActivityLog activityLog)
    : IRequestHandler<GetPatientTimelineQuery, PatientTimelinePageResponse>
{
    public async Task<PatientTimelinePageResponse> Handle(GetPatientTimelineQuery request, CancellationToken cancellationToken)
    {
        _ = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
            ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [request.PatientId], cancellationToken);

        // The audit log has every kind but Appointments and Referrals, and appointments' and referrals' histories only
        // their own: ask each only when the kinds filter wants something from it.
        var kinds = PatientTimelineKinds.Parse(request.Kinds);
        var wantsAudit = kinds.Count == 0
                         || kinds.Any(k => k is not PatientTimelineKind.Appointments
                             and not PatientTimelineKind.Referrals and not PatientTimelineKind.WaitingTime);
        var wantsAppointments = kinds.Count == 0 || kinds.Contains(PatientTimelineKind.Appointments);
        var wantsReferrals = kinds.Count == 0
                             || kinds.Contains(PatientTimelineKind.Referrals)
                             || kinds.Contains(PatientTimelineKind.WaitingTime);

        // Each side gives one more than a page, to tell whether there's another.
        IReadOnlyList<PatientTimelineRow> auditRows = wantsAudit
            ? await auditRepository.GetPatientTimelineAsync(request, cancellationToken)
            : [];
        IReadOnlyList<AppointmentTimelineRow> appointmentRows = wantsAppointments
            ? await appointmentRepository.GetPatientTimelineAsync(request, cancellationToken)
            : [];
        IReadOnlyList<ReferralTimelineRow> referralRows = wantsReferrals
            ? await referralRepository.GetPatientTimelineAsync(request, cancellationToken)
            : [];
        if (request.FullView)
            referralRows = await WithPauseDetailsAsync(referralRows, cancellationToken);

        // Merged newest first. The sort is stable, so entries that share a time keep the order their own search gave
        // them (by ID, as the database orders IDs, which the next page's cursor compares the same way); the sides never
        // share a time in practice, as each change's entries are a tick apart. Together they hold more than a page
        // whenever any side has more.
        var merged = auditRows
            .Select(row => new Entry(new TimelineCursor(row.Entry.AtUtc, row.Entry.Id), row, null))
            .Concat(appointmentRows.Select(row => new Entry(new TimelineCursor(row.Event.AtUtc, row.Event.Id), null, row)))
            .Concat(referralRows.Select(row => new Entry(new TimelineCursor(row.Event.AtUtc, row.Event.Id), null, null, row)))
            .OrderByDescending(e => e.Position.AtUtc)
            .ToList();
        var page = merged.Take(request.PageSize).ToList();

        // Admins see the text of the notes added and edited, from the note versions (the audit holds none).
        if (request.FullView)
        {
            page = await WithNoteTextAsync(request.PatientId, page, cancellationToken);
            page = await WithLettersAsync(page, cancellationToken);
        }

        return new PatientTimelinePageResponse
        {
            // The searches already leave deleted pathways out for staff; this makes sure nothing slips through. A
            // referral's entries are always shown: referrals are never deleted.
            Items = page
                .Where(e => request.FullView
                            || e.Referral is not null
                            || (e.Audit is { } audit
                                ? audit.CurrentPathwayTitle is not null || audit.IsAboutPatient
                                : e.Appointment!.IsVisibleToStaff))
                .Select(e => e switch
                {
                    { Audit: { } audit } => TimelineMapper.ToResponse(audit, request.FullView),
                    { Referral: { } referral } => TimelineMapper.ToResponse(referral, request.FullView),
                    _ => TimelineMapper.ToResponse(e.Appointment!, request.FullView)
                })
                .ToList(),
            NextCursor = merged.Count > request.PageSize ? page[^1].Position.Encode() : null,
        };
    }

    /// <summary>The referral entries with each clock pause's detail (admins only), read from the pauses.</summary>
    private async Task<IReadOnlyList<ReferralTimelineRow>> WithPauseDetailsAsync(
        IReadOnlyList<ReferralTimelineRow> rows, CancellationToken ct)
    {
        var pauseIds = rows
            .Where(r => r.Event.Action == ReferralEventAction.ClockPaused)
            .Select(r => ReferralEventDetails.Parse(r.Event.Details).PauseId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        if (pauseIds.Count == 0)
            return rows;

        var details = await referralRepository.GetPauseDetailsAsync(pauseIds, ct);
        return rows
            .Select(r => r.Event.Action == ReferralEventAction.ClockPaused
                         && ReferralEventDetails.Parse(r.Event.Details).PauseId is { } id
                ? r with { PauseDetail = details.GetValueOrDefault(id) }
                : r)
            .ToList();
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

    /// <summary>
    /// The page with each letter entry's letter, for its free text (admins only): the audit holds none. Only letters of
    /// this patient's entries are asked for, by the IDs their entries hold.
    /// </summary>
    private async Task<List<Entry>> WithLettersAsync(List<Entry> page, CancellationToken ct)
    {
        var ids = page
            .Select(e => e.Audit is { } audit && PatientTimelineKinds.Of(audit.Entry) == PatientTimelineKind.Letters
                ? TimelineMapper.GuidIn(audit.Entry.Details, "letterId")
                : null)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return page;

        var letters = (await letterRepository.GetByIdsAsync(ids, ct)).ToDictionary(l => l.Id);
        return page
            .Select(e => e.Audit is { } audit
                         && PatientTimelineKinds.Of(audit.Entry) == PatientTimelineKind.Letters
                         && TimelineMapper.GuidIn(audit.Entry.Details, "letterId") is { } id
                         && letters.TryGetValue(id, out var letter) && letter.PatientId == audit.Entry.PatientId
                ? e with { Audit = audit with { Letter = letter } }
                : e)
            .ToList();
    }

    /// <summary>One entry of the merged feed, from the audit log, appointments' history or referrals' history.</summary>
    private sealed record Entry(
        TimelineCursor Position,
        PatientTimelineRow? Audit,
        AppointmentTimelineRow? Appointment,
        ReferralTimelineRow? Referral = null);
}
