using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;

namespace MedApp.Application.Clinics.Common;

/// <summary>Clinic sessions and patterns as the API returns them.</summary>
public static class ClinicMapper
{
    /// <summary>
    /// A session (loaded with its room, the room's site and its specialty), with its slots counted from the starts of
    /// the appointments booked into it. The caller can change it when they manage clinics (<paramref name="canManage"/>),
    /// it's Planned and its day isn't past.
    /// </summary>
    public static ClinicSessionResponse ToResponse(
        ClinicSession session, string staffUsername, IEnumerable<DateTime> bookedStarts, DateTime nowUtc, bool canManage)
    {
        var starts = bookedStarts.ToList();
        var (booked, _) = ClinicRules.Counts(session, starts, nowUtc);
        var free = session.Status == ClinicSessionStatus.Planned ? ClinicRules.FreeStarts(session, starts, nowUtc) : [];
        return new ClinicSessionResponse
        {
            Id = session.Id,
            StaffUserId = session.StaffUserId,
            StaffUsername = staffUsername,
            RoomId = session.RoomId,
            RoomName = session.Room?.Name ?? string.Empty,
            SiteId = session.Room?.SiteId ?? Guid.Empty,
            SiteName = session.Room?.Site?.Name ?? string.Empty,
            SpecialtyId = session.SpecialtyId,
            SpecialtyName = session.Specialty?.Name ?? string.Empty,
            PatternId = session.PatternId,
            Date = session.Date,
            StartUtc = session.StartUtc,
            EndUtc = session.EndUtc,
            SlotMinutes = session.SlotMinutes,
            SlotCount = ClinicRules.Slots(session).Count,
            BookedCount = booked,
            FreeSlotCount = free.Count,
            FreeSlotStartsUtc = free.ToList(),
            Status = session.Status.ToString(),
            CancelReason = session.CancelReason,
            CancelledAtUtc = session.CancelledAtUtc,
            LastUpdated = session.LastUpdated,
            CanChange = canManage && session.Status == ClinicSessionStatus.Planned && session.Date >= UkTime.DateOf(nowUtc)
        };
    }

    /// <summary>
    /// A session with each of its slots: free, or the appointment on the calendar in it (with its patient); and, once
    /// it's cancelled, the patients whose appointments were cancelled with it, to tell.
    /// </summary>
    public static ClinicSessionDetailResponse ToDetailResponse(
        ClinicSession session, string staffUsername, IReadOnlyList<Appointment> appointments, DateTime nowUtc, bool canManage)
    {
        var byStart = appointments
            .Where(a => a.StartUtc is not null && AppointmentWorkflow.IsOnCalendar(a.Status))
            .GroupBy(a => UkTime.Utc(a.StartUtc!.Value))
            .ToDictionary(g => g.Key, g => g.First());

        return new ClinicSessionDetailResponse
        {
            Session = ToResponse(session, staffUsername, byStart.Keys, nowUtc, canManage),
            Slots = ClinicRules.Slots(session)
                .Select(slot =>
                {
                    var appointment = byStart.GetValueOrDefault(slot.StartUtc);
                    return new ClinicSlotResponse
                    {
                        StartUtc = slot.StartUtc,
                        EndUtc = slot.EndUtc,
                        IsPast = slot.StartUtc <= nowUtc,
                        AppointmentId = appointment?.Id,
                        PatientId = appointment?.PatientId,
                        PatientName = appointment is null ? null : PathwayMapper.PatientName(appointment.Patient),
                        AppointmentStatus = appointment?.Status.ToString()
                    };
                })
                .ToList(),
            PatientsToTell = session.Status == ClinicSessionStatus.Cancelled
                ? appointments.Where(a => a.Status == AppointmentStatus.Cancelled).OrderBy(a => a.StartUtc).Select(ToPatientToTell).ToList()
                : []
        };
    }

    /// <summary>A patient to tell that their appointment was cancelled with its session.</summary>
    public static PatientToTellResponse ToPatientToTell(Appointment appointment) => new()
    {
        AppointmentId = appointment.Id,
        PatientId = appointment.PatientId,
        PatientName = PathwayMapper.PatientName(appointment.Patient),
        NhsNumber = appointment.Patient?.NhsNumber,
        StartUtc = appointment.StartUtc ?? default
    };

    /// <summary>A pattern (loaded with its room, the room's site and its specialty); it has ended once its until date is past.</summary>
    public static ClinicPatternResponse ToResponse(ClinicPattern pattern, string staffUsername, DateOnly today) => new()
    {
        Id = pattern.Id,
        StaffUserId = pattern.StaffUserId,
        StaffUsername = staffUsername,
        RoomId = pattern.RoomId,
        RoomName = pattern.Room?.Name ?? string.Empty,
        SiteId = pattern.Room?.SiteId ?? Guid.Empty,
        SiteName = pattern.Room?.Site?.Name ?? string.Empty,
        SpecialtyId = pattern.SpecialtyId,
        SpecialtyName = pattern.Specialty?.Name ?? string.Empty,
        DayOfWeek = pattern.DayOfWeek.ToString(),
        Starts = pattern.Starts,
        Ends = pattern.Ends,
        SlotMinutes = pattern.SlotMinutes,
        EveryWeeks = pattern.EveryWeeks,
        FromDate = pattern.FromDate,
        UntilDate = pattern.UntilDate,
        HasEnded = pattern.UntilDate is { } until && until < today,
        LastUpdated = pattern.LastUpdated
    };
}
