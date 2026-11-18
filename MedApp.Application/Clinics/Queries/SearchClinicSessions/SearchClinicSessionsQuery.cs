using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Queries.SearchClinicSessions;

/// <summary>
/// Clinic sessions starting from one UK date to another (at most 62 days), by start, optionally narrowed by clinician,
/// site, room or specialty; with <paramref name="WithFreeSlots"/>, only those with a slot free and not started (the
/// booking dialog's list). Names no patient, so it records nothing in the activity log.
/// </summary>
public sealed record SearchClinicSessionsQuery(
    DateOnly From,
    DateOnly To,
    Guid? StaffUserId,
    Guid? SiteId,
    Guid? RoomId,
    Guid? SpecialtyId,
    bool IncludeCancelled,
    bool WithFreeSlots,
    CalendarActor Actor)
    : IRequest<IReadOnlyList<ClinicSessionResponse>>;
