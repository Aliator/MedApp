using MediatR;
using MedApp.Application.Appointments.Commands.CreateAppointment;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Commands.CancelClinicSession;
using MedApp.Application.Clinics.Commands.CreateClinicPattern;
using MedApp.Application.Clinics.Commands.CreateSessionsFromPattern;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Domain.Sites;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Clinic sessions (<c>docs/centre-phases-3-5-plan.md</c>, section 3.4), through the app's commands: the service
/// manager sets up a weekly pattern for each specialty's clinic three weeks ago; the booking coordinator makes their
/// sessions for the next eight weeks through Create sessions (which places each through <c>SessionPlacement</c>, so a
/// date whose time is already taken on the clinician's calendar, or the room's, is skipped, saying why); then books
/// reviews into some of the coming slots, and cancels one session that had bookings, leaving its call list. The
/// history before the patterns (the appointments the pathways booked) stays as it was booked, outside sessions.
/// </summary>
public static class DemoClinics
{
    /// <summary>Each specialty's weekly clinic: who, where, when and its slots.</summary>
    public static readonly IReadOnlyList<(string Username, string Specialty, string Room, DayOfWeek Day, TimeOnly Starts, TimeOnly Ends, int Slot)> Patterns =
    [
        (DemoStaff.DermatologistUsername, DemoSpecialties.Dermatology, "Clinic room 1", DayOfWeek.Monday, new(9, 0), new(12, 0), 20),
        (DemoStaff.SkinGpUsername, DemoSpecialties.Dermatology, "Clinic room 2", DayOfWeek.Wednesday, new(13, 40), new(16, 40), 20),
        (DemoStaff.GynaecologistUsername, DemoSpecialties.Gynaecology, "Clinic room 3", DayOfWeek.Tuesday, new(9, 0), new(12, 0), 30),
        (DemoStaff.UrologistUsername, DemoSpecialties.Urology, "Clinic room 3", DayOfWeek.Wednesday, new(13, 30), new(16, 30), 30),
        (DemoStaff.DiabetesNurseUsername, DemoSpecialties.Diabetes, "Clinic room 1", DayOfWeek.Thursday, new(8, 30), new(12, 30), 30),
        (DemoStaff.PodiatristUsername, DemoSpecialties.Biomechanics, "Gait lab", DayOfWeek.Thursday, new(9, 0), new(12, 0), 45),
    ];

    public static async Task<(object Counts, object Tour)> CreateAsync(SeedServices services, DemoWorld world, IReadOnlyList<PatientSeed> patients, CancellationToken ct)
    {
        var timeline = world.Timeline;
        var now = timeline.NowUtc;
        var random = world.Random;
        var manager = world.Manager;
        var coordinator = world.Coordinator;

        Task<T> Send<T>(DateTime at, StaffMember by, IRequest<T> request)
        {
            services.At(at, by.ActivityActor);
            return services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(request, ct)));
        }

        CalendarActor Calendar(StaffMember by, DateTime at) => new(by.Id, by.Username, by.PermissionsAt(at));

        // The patterns, three weeks ago.
        var setUpAt = DemoTimeline.WorkingMoment(now.AddDays(-21), 10, 0);
        var patternIds = new List<Guid>();
        foreach (var (username, specialty, room, day, starts, ends, slot) in Patterns)
        {
            var roomId = world.Setup.Rooms.Single(r => r.Key.EndsWith($": {room}", StringComparison.Ordinal)).Value;
            var pattern = await Send(setUpAt, manager, new CreateClinicPatternCommand(world.Member(username).Id, roomId,
                world.Specialty(specialty).Id, day.ToString(), starts, ends, slot, 1, UkTime.DateOf(setUpAt), null, Calendar(manager, setUpAt)));
            patternIds.Add(pattern.Id);
            setUpAt = setUpAt.AddMinutes(4);
        }

        // Their sessions for the next eight weeks, made by the coordinator two weeks ago.
        var madeAt = DemoTimeline.WorkingMoment(now.AddDays(-14), 9, 15);
        var sessions = new List<ClinicSessionResponse>();
        var skipped = 0;
        foreach (var id in patternIds)
        {
            var made = await Send(madeAt, coordinator, new CreateSessionsFromPatternCommand(id, 8, Calendar(coordinator, madeAt)));
            sessions.AddRange(made.Created);
            skipped += made.Skipped.Count;
            madeAt = madeAt.AddMinutes(2);
        }

        // Reviews booked into some of the coming slots, over the last fortnight.
        var reviewers = patients.Where(p => p.Role == PatientRole.Active && p.Id != Guid.Empty && !p.Restricted && p.AnonymisedUtc is null).ToList();
        var booked = new Dictionary<Guid, int>();
        var bookedAt = DemoTimeline.WorkingMoment(now.AddDays(-13), 10, 0);
        foreach (var session in sessions.Where(s => s.StartUtc > now.AddHours(2)).OrderBy(s => s.StartUtc))
        {
            for (var start = session.StartUtc; start.AddMinutes(session.SlotMinutes) <= session.EndUtc; start = start.AddMinutes(session.SlotMinutes))
            {
                if (random.NextDouble() >= 0.45)
                    continue;
                var at = bookedAt < now ? bookedAt : now.AddMinutes(-30);
                var patient = reviewers[random.Next(reviewers.Count)];
                if (patient.CreatedUtc > at)
                    continue;
                await Send(at, coordinator, new CreateAppointmentCommand(patient.Id, null, null, session.StaffUserId, start, start.AddMinutes(session.SlotMinutes),
                    ReviewNotes[random.Next(ReviewNotes.Length)], coordinator.ActorAt(at), session.Id));
                booked[session.Id] = booked.GetValueOrDefault(session.Id) + 1;
                bookedAt = DemoTimeline.InWorkingHours(bookedAt.AddMinutes(random.Next(20, 240)), random);
            }
        }

        // One session cancelled (the clinician is away that day): its bookings cancelled with it, a call list left.
        object? cancelled = null;
        Guid? cancelledId = null;
        if (sessions.Where(s => booked.GetValueOrDefault(s.Id) >= 2 && s.StartUtc > now.AddDays(3)).OrderBy(s => s.StartUtc).FirstOrDefault() is { } toCancel)
        {
            // Yesterday in working hours (never after now: at a weekend or just after midnight, that's later than now).
            var at = DemoTimeline.InWorkingHours(now.AddDays(-1), random);
            if (at >= now)
                at = now.AddHours(-2);
            var lastUpdated = await services.Run(sp => sp.GetRequiredService<MedAppDbContext>().ClinicSessions
                .Where(s => s.Id == toCancel.Id).Select(s => s.LastUpdated).SingleAsync(ct));
            var result = await Send(at, coordinator, new CancelClinicSessionCommand(toCancel.Id,
                "The clinician is away at a conference that day.", lastUpdated, Calendar(coordinator, at)));
            cancelledId = toCancel.Id;
            cancelled = new { clinician = toCancel.StaffUsername, toCancel.Date, at = UkTime.DescribeTime(toCancel.StartUtc), callList = result.PatientsToTell.Count };
        }

        var next = sessions.Where(s => s.StartUtc > now && booked.ContainsKey(s.Id) && s.Id != cancelledId).OrderBy(s => s.StartUtc).FirstOrDefault();
        return (new
        {
            patterns = patternIds.Count,
            sessions = sessions.Count,
            sessionsSkipped = skipped,
            appointmentsInSessions = booked.Values.Sum(),
            cancelledSessions = cancelled is null ? 0 : 1,
        }, new
        {
            cancelledSessionWithItsCallList = cancelled,
            nextSessionWithBookings = next is null ? null : new { clinician = next.StaffUsername, next.Date, at = UkTime.DescribeTime(next.StartUtc), room = next.RoomName, site = next.SiteName },
        });
    }

    private static readonly string[] ReviewNotes =
    [
        "Review: booked from the waiting list.",
        "Six-month review, asked for at the last visit.",
        "Review of the new treatment.",
        "Results review: the patient asked to come in.",
    ];
}
