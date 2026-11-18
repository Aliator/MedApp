using MedApp.Application.Calendar.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;
using MedApp.Domain.Sites;

namespace MedApp.Application.Clinics.Common;

/// <summary>
/// Everything that decides whether a room is free on one UK date, as the repository loads it: the room with its site,
/// the site's opening hours valid that date, the closed days (everywhere and the site's), the room's Planned sessions and
/// its appointments on the calendar (Booked, Attended or Did not attend) that day.
/// </summary>
public sealed record RoomDay(
    Room Room,
    DateOnly Date,
    IReadOnlyList<SiteHours> SiteHours,
    ClosedDaySet ClosedDays,
    IReadOnlyList<ClinicSession> Sessions,
    IReadOnlyList<Appointment> Appointments);

/// <summary>Which sessions a list shows: those starting from one UK date to another, narrowed by whoever is given.</summary>
public sealed record ClinicSessionFilter(
    DateOnly From,
    DateOnly To,
    Guid? StaffUserId = null,
    Guid? SiteId = null,
    Guid? RoomId = null,
    Guid? SpecialtyId = null,
    bool IncludeCancelled = false);
