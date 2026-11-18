using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using MedApp.Domain.Clinics;
using MedApp.Domain.Sites;
using MedApp.Domain.Specialties;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Synthetic clinic sessions and patterns: <see cref="CalendarTestConstants.StaffUserId"/>'s Dermatology clinic in Room 2
/// at the Northgate outreach clinic, next Monday 09:00 to 12:00 UK time in 20-minute slots (nine of them).
/// </summary>
public static class ClinicsTestConstants
{
    public static readonly Guid SessionId = Guid.Parse("0000c1c0-0000-0000-0000-000000000001");
    public static readonly Guid OtherSessionId = Guid.Parse("0000c1c0-0000-0000-0000-000000000002");
    public static readonly Guid PatternId = Guid.Parse("0000c1c0-0000-0000-0000-000000000010");
    public static readonly Guid SpecialtyId = Guid.Parse("0000c1c0-0000-0000-0000-000000000020");
    public const string SpecialtyName = "Synthetic dermatology";
    public const string CancelReason = "Synthetic reason: clinician away.";

    public static Site Site() => SitesTestConstants.NewSite();

    public static Room Room(Guid? id = null, string name = SitesTestConstants.RoomName, bool isArchived = false)
    {
        var room = SitesTestConstants.NewRoom(id, name: name, isArchived: isArchived);
        room.Site = Site();
        return room;
    }

    public static Specialty Specialty(bool isArchived = false) => SpecialtiesTestConstants.CreateSpecialty(
        SpecialtyName, isArchived, id: SpecialtyId);

    /// <summary>A Planned session, by default next Monday 09:00 to 12:00 in 20-minute slots, in Room 2.</summary>
    public static ClinicSession CreateSession(
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        int slotMinutes = 20,
        Room? room = null,
        Guid? id = null,
        Guid? staffUserId = null,
        ClinicSessionStatus status = ClinicSessionStatus.Planned)
    {
        var start = startUtc ?? At(NextMonday, 9);
        room ??= Room();
        return new ClinicSession
        {
            Id = id ?? SessionId,
            StaffUserId = staffUserId ?? StaffUserId,
            RoomId = room.Id,
            Room = room,
            SpecialtyId = SpecialtyId,
            Specialty = Specialty(),
            Date = UkTime.DateOf(start),
            StartUtc = start,
            EndUtc = endUtc ?? At(NextMonday, 12),
            SlotMinutes = slotMinutes,
            Status = status,
            CreatedAtUtc = Now.AddDays(-1),
            CreatedByUserId = AdminUserId,
            LastUpdated = Now.AddDays(-1)
        };
    }

    /// <summary>An appointment booked into a session's slot (its clinician, room and slot), for a synthetic patient.</summary>
    public static Appointment InSession(ClinicSession session, DateTime slotStartUtc, AppointmentStatus status = AppointmentStatus.Booked)
    {
        var appointment = CreateAppointment(status, slotStartUtc, session.SlotMinutes);
        appointment.ClinicSessionId = session.Id;
        appointment.RoomId = session.RoomId;
        appointment.StaffUserId = session.StaffUserId;
        return appointment;
    }

    /// <summary>What a room has on a date: nothing, by default (the site open the default hours).</summary>
    public static RoomDay CreateRoomDay(
        DateOnly? date = null,
        Room? room = null,
        IEnumerable<ClinicSession>? sessions = null,
        IEnumerable<Appointment>? appointments = null,
        IEnumerable<SiteHours>? siteHours = null,
        IEnumerable<ClosedDay>? closedDays = null) =>
        new(
            room ?? Room(),
            date ?? NextMonday,
            (siteHours ?? []).ToList(),
            new ClosedDaySet(closedDays ?? []),
            (sessions ?? []).ToList(),
            (appointments ?? []).ToList());

    /// <summary>A pattern: Mondays 09:00 to 12:00, 20-minute slots, every week from today with no end.</summary>
    public static ClinicPattern CreatePattern(
        int everyWeeks = 1,
        DateOnly? fromDate = null,
        DateOnly? untilDate = null,
        DayOfWeek dayOfWeek = DayOfWeek.Monday,
        Room? room = null) => new()
    {
        Id = PatternId,
        StaffUserId = StaffUserId,
        RoomId = (room ?? Room()).Id,
        Room = room ?? Room(),
        SpecialtyId = SpecialtyId,
        Specialty = Specialty(),
        DayOfWeek = dayOfWeek,
        Starts = new TimeOnly(9, 0),
        Ends = new TimeOnly(12, 0),
        SlotMinutes = 20,
        EveryWeeks = everyWeeks,
        FromDate = fromDate ?? Today,
        UntilDate = untilDate,
        CreatedAtUtc = Now.AddDays(-1),
        CreatedByUserId = AdminUserId,
        LastUpdated = Now.AddDays(-1)
    };
}
