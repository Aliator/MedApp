using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Replays every planned pathway in the order they were referred (<see cref="PathwayReplay"/>), adds the
/// appointments that aren't for a pathway, the imported records from before the app, and saves it all in a few
/// batches.
/// </summary>
public static class DemoPathways
{
    /// <summary>How many appointments the lead has on the diary day: a full clinic, with time left to book into.</summary>
    public const int LeadAppointmentsToday = 5;

    /// <summary>How much slower the archived pathway's last few go, so they're still finishing months after it was replaced.</summary>
    private const double StragglerScale = 6.0;

    public static void Replay(DemoWorld world, IReadOnlyList<PathwayPlan> plans)
    {
        var leaver = world.Staff.Single(s => s.Kind == StaffKind.Leaver);
        var leftUtc = leaver.LeftUtc!.Value;
        var oldPathwayDoneBy = world.Timeline.NowUtc.AddDays(-29);

        foreach (var plan in plans)
        {
            var lead = ChooseLead(world, plan, allowLeaver: true);
            var scale = plan.Straggler ? StragglerScale : 1.0;

            for (var attempt = 0; ; attempt++)
            {
                var checkpoint = world.Allocator.Checkpoint();
                var replay = new PathwayReplay(world, plan, lead, scale);
                replay.Run();

                // The leaver's work all closed before they left: never open work with a deleted account (H-21).
                var leaverProblem = replay.Lead.Id == leaver.Id
                                    && (!PathwayWorkflow.IsClosed(replay.Pathway.Status) || replay.Involves(leaver, leftUtc));
                // The archived template's pathways finished well before the last four weeks, but for the stragglers.
                var oldPathwayProblem = plan.PathwayKind == PathwayKind.SkinLesion2024 && !plan.Straggler
                                        && (!PathwayWorkflow.IsClosed(replay.Pathway.Status) || replay.LastActivityUtc > oldPathwayDoneBy);

                if ((!leaverProblem && !oldPathwayProblem) || attempt >= 4)
                {
                    replay.Commit();
                    break;
                }

                world.Allocator.Rollback(checkpoint);
                if (leaverProblem)
                    lead = ChooseLead(world, plan, allowLeaver: false);
                else
                    scale *= 0.5;
            }
        }
    }

    /// <summary>Who leads a pathway: the right clinician for it; the leaver only for work long before they left.</summary>
    private static StaffMember ChooseLead(DemoWorld world, PathwayPlan plan, bool allowLeaver)
    {
        var created = plan.CreatedUtc;
        var leaver = world.Staff.Single(s => s.Kind == StaffKind.Leaver);
        var leaverWeight = allowLeaver && leaver.WorksAt(created) && created < leaver.LeftUtc!.Value.AddDays(-7 * 12) ? 30 : 0;

        var dermatologist = world.Member(DemoStaff.DermatologistUsername);
        var skinGp = world.Member(DemoStaff.SkinGpUsername);
        (StaffMember, int)[] candidates = plan.PathwayKind switch
        {
            PathwayKind.SkinLesion or PathwayKind.SkinLesion2024 => [(dermatologist, 50), (skinGp, 50), (leaver, leaverWeight)],
            PathwayKind.InflammatorySkin => [(skinGp, 65), (dermatologist, 35), (leaver, leaverWeight)],
            PathwayKind.PostMenopausalBleeding => [(world.Member(DemoStaff.GynaecologistUsername), 1)],
            PathwayKind.Prostate => [(world.Member(DemoStaff.UrologistUsername), 1)],
            PathwayKind.Diabetes => [(world.Member(DemoStaff.DiabetesNurseUsername), 60), (world.Lead, 40)],
            _ => [(world.Member(DemoStaff.PodiatristUsername), 1)],
        };

        return PathwayPlans.Weighted(world.Random, candidates.Where(c => c.Item2 > 0).ToArray());
    }

    /// <summary>
    /// Appointments that aren't for a pathway: telephone reviews and advice clinics, booked by the coordinator as
    /// CreateAppointmentHandler books them. One fills the pharmacist's removed admin time; it's booked (and so
    /// reserved) before the pathways are replayed, so nothing else takes that time.
    /// </summary>
    public static void BookFreedSlot(DemoWorld world, IReadOnlyList<PatientSeed> patients)
    {
        if (world.Calendars.FreedSlot is not { } freed)
            return;

        var pharmacist = world.Staff.Single(s => s.Id == freed.StaffUserId);
        var bookedAt = freed.StartUtc.AddDays(-3).AddHours(-2);
        Standalone(world, Pick(world, patients, bookedAt), pharmacist, freed.StartUtc, freed.EndUtc, world.Coordinator, bookedAt,
            "Telephone review: the patient has a question about a new medicine.");
    }

    /// <summary>A few standalone appointments over the last eight weeks and the next three, and the lead's day topped up to a full clinic.</summary>
    public static void BookStandalone(DemoWorld world, IReadOnlyList<PatientSeed> patients)
    {
        var random = world.Random;
        var now = world.Timeline.NowUtc;
        string[] notes =
        [
            "Advice clinic: the GP's letter is on the record.",
            "Telephone review of symptoms.",
            "Telephone review: results gone through.",
            "Dressing clinic.",
            "Review asked for by the GP.",
        ];
        var clinicians = new[]
        {
            world.Lead, world.Member(DemoStaff.DermatologistUsername), world.Member(DemoStaff.SkinGpUsername),
            world.Member(DemoStaff.SkinCancerNurseUsername), world.Member(DemoStaff.DiabetesNurseUsername),
            world.Member(DemoStaff.PharmacistUsername), world.Member(DemoStaff.PodiatristUsername),
        };

        for (var i = 0; i < 18; i++)
        {
            var staff = clinicians[random.Next(clinicians.Length)];
            var bookedAt = DemoTimeline.InWorkingHours(now.AddDays(-random.Next(3, 60)), random);
            var slot = world.Allocator.Find(staff.Id, bookedAt.AddDays(random.Next(2, 14)), random.NextDouble() < 0.5 ? 20 : 30, random,
                world.Calendars.Prefers(DayKind.Clinic));
            if (slot is null)
                continue;
            Standalone(world, Pick(world, patients, bookedAt), staff, slot.StartUtc, slot.EndUtc, world.Coordinator, bookedAt, notes[random.Next(notes.Length)]);
        }

        // The lead's diary day: a full clinic of five, around lunch and the day's meetings, with free time left.
        var day = world.Timeline.DiaryDay;
        var lead = world.Lead;
        var have = world.Appointments.Count(a => a.StaffUserId == lead.Id && a.StartUtc is { } s && UkTime.DateOf(s) == day
                                               && AppointmentWorkflow.IsOnCalendar(a.Status));
        for (var i = have; i < LeadAppointmentsToday; i++)
        {
            var minutes = random.NextDouble() < 0.5 ? 20 : 30;
            var free = world.Allocator.FreeStarts(lead.Id, day, minutes, WorkingHours.DayStartUtc(day));
            if (free.Count == 0)
                break;
            var start = free[random.Next(free.Count)];
            var bookedAt = DemoTimeline.InWorkingHours(start.AddDays(-random.Next(4, 15)), random);
            Standalone(world, Pick(world, patients, bookedAt), lead, start, start.AddMinutes(minutes), world.Coordinator, bookedAt, notes[random.Next(notes.Length)]);
        }

        WalkIn(world, patients);
    }

    /// <summary>
    /// One walk-in (decision 74): a patient seen without a booking by the skin cancer nurse specialist on the last
    /// working day before today, recorded after the event, the next working morning, by the booking coordinator,
    /// through <see cref="AppointmentChange.RecordAfterEvent"/>. It's on the calendar flagged "Recorded after the
    /// event", and the nurse has the "Appointment recorded" notification.
    /// </summary>
    private static void WalkIn(DemoWorld world, IReadOnlyList<PatientSeed> patients)
    {
        var nurse = world.Member(DemoStaff.SkinCancerNurseUsername);
        var day = world.Timeline.Today.AddDays(-1);
        while (!DemoClosedDays.IsWorkingDay(day))
            day = day.AddDays(-1);
        var free = world.Allocator.FreeStarts(nurse.Id, day, 20, WorkingHours.DayStartUtc(day));
        if (free.Count == 0)
            return;

        var start = free[free.Count / 2];
        var end = start.AddMinutes(20);
        var recordedAt = DemoTimeline.WorkingMoment(end.AddDays(1), 8, 40);
        if (recordedAt >= world.Timeline.NowUtc)
            recordedAt = end.AddMinutes(45) < world.Timeline.NowUtc ? end.AddMinutes(45) : world.Timeline.NowUtc.AddMinutes(-5);
        if (recordedAt <= end)
            return;

        world.Allocator.Reserve(nurse.Id, start, end);
        var by = world.Coordinator;
        var patient = Pick(world, patients, start);
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Status = AppointmentStatus.Requested,
            Notes = "Walked in worried about a biopsy dressing; dressing changed.",
            IsAutomatic = false,
            CreatedAtUtc = recordedAt,
            CreatedByUserId = by.Id,
            LastUpdated = recordedAt,
        };
        var change = new AppointmentChange(appointment, by.Actor, recordedAt);
        change.RecordAfterEvent(nurse.Id, nurse.Username, start, end, AppointmentStatus.Attended);
        world.Appointments.Add(appointment);
        world.AppointmentEvents.AddRange(change.Events);
        world.Touches.Add(new Touch(by.Id, recordedAt, patient.Id, null));
        if (recordedAt >= world.Timeline.NotificationsFromUtc)
            world.Notifications.AddRange(AppointmentNotifications.AfterRecording(appointment, by.Actor, recordedAt));
    }

    /// <summary>
    /// Books one, reserving its time on the allocator first, so nothing booked later can take it (and booking a time
    /// that isn't free fails loudly, as the app would refuse it).
    /// </summary>
    private static void Standalone(DemoWorld world, PatientSeed patient, StaffMember staff, DateTime startUtc, DateTime endUtc,
        StaffMember by, DateTime bookedAt, string note)
    {
        world.Allocator.Reserve(staff.Id, startUtc, endUtc);
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Status = AppointmentStatus.Requested,
            Notes = note,
            IsAutomatic = false,
            CreatedAtUtc = bookedAt,
            CreatedByUserId = by.Id,
            LastUpdated = bookedAt,
        };
        var change = new AppointmentChange(appointment, by.Actor, bookedAt);
        change.Book(staff.Id, staff.Username, startUtc, endUtc);
        world.Appointments.Add(appointment);
        world.AppointmentEvents.AddRange(change.Events);
        world.Touches.Add(new Touch(by.Id, bookedAt, patient.Id, null));
        if (bookedAt >= world.Timeline.NotificationsFromUtc)
            world.Notifications.AddRange(AppointmentNotifications.AfterBooking(appointment, AppointmentStatus.Requested, null, null, by.Actor, bookedAt));

        // Past ones were attended (now and then not), and recorded by the clinician soon after.
        var outcomeAt = endUtc.AddMinutes(world.Random.Next(3, 40));
        if (outcomeAt < world.Timeline.NowUtc)
        {
            var outcome = new AppointmentChange(appointment, staff.Actor, outcomeAt, staff.Username);
            outcome.RecordOutcome(world.Random.NextDouble() < 0.1 ? AppointmentStatus.DidNotAttend : AppointmentStatus.Attended);
            world.AppointmentEvents.AddRange(outcome.Events);
            world.Touches.Add(new Touch(staff.Id, outcomeAt, patient.Id, null));
        }
    }

    /// <summary>A patient to book: anyone referred or waiting who was registered by the time it's booked.</summary>
    private static PatientSeed Pick(DemoWorld world, IReadOnlyList<PatientSeed> patients, DateTime bookedAt)
    {
        var registered = patients.Where(p => p.Role is PatientRole.Active or PatientRole.Waiting && p.IsOnSystemAt(bookedAt)).ToList();
        return registered[world.Random.Next(registered.Count)];
    }

    /// <summary>
    /// The imported patients' records from the older system: closed long ago, with every stage finished. They predate
    /// MedApp, so there's no audit history (the app's history starts when it was used) and no visits (their stages
    /// were copied before stages could involve one, so they're None, as the migration left existing stages).
    /// </summary>
    public static void AddImported(DemoWorld world, IReadOnlyList<PatientSeed> imported)
    {
        var random = world.Random;
        foreach (var patient in imported)
        {
            var count = random.NextDouble() < 0.3 ? 2 : 1;
            for (var i = 0; i < count; i++)
            {
                // The skin clinic's old records: a lesion seen and sampled (for a child, a skin review), sometimes a review later.
                var lesion = i == 0 && !patient.IsChildAt(patient.LastUpdatedUtc);
                var spec = DemoPathwayKinds.For(lesion ? PathwayKind.SkinLesion2024 : PathwayKind.InflammatorySkin);
                var kinds = ProcedureKinds.For(spec.PathwayKind);
                var kind = kinds[random.Next(kinds.Count)];
                var finished = patient.LastUpdatedUtc.AddDays(-random.Next(0, 40) - 60 * i);
                var created = finished.AddDays(-random.Next(40, 160));
                var id = Guid.NewGuid();
                var stages = spec.Stages.Select((s, index) =>
                {
                    var definition = world.Definitions[s.Definition];
                    var done = created.AddDays((finished - created).TotalDays * (index + 1) / spec.Stages.Length);
                    return new PatientPathwayStage
                    {
                        Id = Guid.NewGuid(),
                        PatientPathwayId = id,
                        StageDefinitionId = definition.Id,
                        StageOrder = index + 1,
                        Name = definition.Name,
                        Description = definition.Description,
                        Instructions = definition.Instructions,
                        VisitRequirement = VisitRequirement.None,
                        Notes = "Imported from the previous system: see the scanned record.",
                        StartedAtUtc = done.AddDays(-1),
                        IsCompleted = true,
                        IsLocked = true,
                        CompletedAtUtc = done,
                        CreatedAt = created,
                        LastUpdated = done,
                    };
                }).ToList();

                // Its note came across with it, from before MedApp recorded who wrote what: no author ("Unknown"), as the
                // migration from the single notes field leaves a pathway with no Created entry.
                const string importedNote = "Imported from the previous system.";
                var note = new PatientPathwayNote
                {
                    Id = Guid.NewGuid(),
                    PatientPathwayId = id,
                    AuthorUserId = null,
                    Text = importedNote,
                    CreatedAtUtc = created,
                    Version = 1,
                };
                world.NoteVersions.Add(new PatientPathwayNoteVersion
                {
                    Id = Guid.NewGuid(),
                    NoteId = note.Id,
                    Version = 1,
                    PatientPathwayId = id,
                    PatientId = patient.Id,
                    Text = importedNote,
                    UserId = null,
                    Username = string.Empty,
                    AtUtc = created,
                });

                world.Pathways.Add(new PatientPathway
                {
                    Id = id,
                    PatientId = patient.Id,
                    TemplateId = null,
                    TemplateName = lesion ? "Imported record: skin lesion clinic" : "Imported record: skin review clinic",
                    Title = kind.Title,
                    Notes = [note],
                    DueDateUtc = DueDates.EndOfDayUtc(DueDates.ToUkDate(created).AddDays(42)),
                    Priority = PatientPathwayPriority.Medium,
                    Status = random.NextDouble() < 0.7 ? Status.Archived : Status.Completed,
                    CreatedAt = created,
                    LastUpdated = finished,
                    Stages = stages,
                });
            }
        }
    }

    /// <summary>
    /// Marks notifications read as people would have: nearly all from over ten days ago read; most from the
    /// last five days not yet.
    /// </summary>
    public static void MarkRead(DemoWorld world)
    {
        var now = world.Timeline.NowUtc;
        foreach (var notification in world.Notifications.OrderBy(n => n.CreatedAtUtc))
        {
            var age = now - notification.CreatedAtUtc;
            var read = world.Random.NextDouble() < (age.TotalDays switch { > 10 => 0.95, > 5 => 0.7, _ => 0.25 });
            if (!read)
                continue;
            var at = notification.CreatedAtUtc.AddHours(0.2 + world.Random.NextDouble() * Math.Min(40, age.TotalHours * 0.8));
            notification.ReadAtUtc = at < now ? at : null;
        }
    }

    /// <summary>Saves the replayed history in batches, parents before children, without tracking each change.</summary>
    public static async Task SaveAsync(SeedServices services, DemoWorld world, CancellationToken ct)
    {
        await Batch(world.Pathways, (db, items) => db.PatientPathways.AddRange(items), 400);
        await Batch(world.Appointments, (db, items) => db.Appointments.AddRange(items), 2000);
        await Batch(world.AppointmentEvents, (db, items) => db.AppointmentEvents.AddRange(items), 4000);
        await Batch(world.Audit, (db, items) => db.PatientPathwayAuditEntries.AddRange(items), 4000);
        await Batch(world.NoteVersions, (db, items) => db.PatientPathwayNoteVersions.AddRange(items), 4000);
        await Batch(world.Notifications, (db, items) => db.Notifications.AddRange(items), 4000);

        async Task Batch<T>(IReadOnlyList<T> all, Action<MedAppDbContext, IEnumerable<T>> add, int size)
        {
            for (var i = 0; i < all.Count; i += size)
            {
                var chunk = all.Skip(i).Take(size).ToList();
                await services.Run(async sp =>
                {
                    var db = sp.GetRequiredService<MedAppDbContext>();
                    db.ChangeTracker.AutoDetectChangesEnabled = false;
                    add(db, chunk);
                    await db.SaveChangesAsync(ct);
                });
            }
        }
    }

    /// <summary>Counts, for the summary.</summary>
    public static object Summarise(DemoWorld world)
    {
        var now = world.Timeline.NowUtc;
        var pathways = world.Pathways;
        return new
        {
            total = pathways.Count,
            byStatus = pathways.GroupBy(p => p.Status).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            byTemplate = pathways.GroupBy(p => p.TemplateName).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            overdue = pathways.Count(p => PathwayWorkflow.IsOverdue(p, now)),
            deleted = world.Deletions.Count,
            stageBypasses = world.Bypasses,
            auditEntries = world.Audit.Count,
            notes = new
            {
                total = pathways.Sum(p => p.Notes.Count),
                edited = pathways.Sum(p => p.Notes.Count(n => n.Version > 1)),
                versions = world.NoteVersions.Count,
                pathwaysWithSeveral = pathways.Count(p => p.Notes.Count > 1),
            },
            appointments = new
            {
                total = world.Appointments.Count,
                byStatus = world.Appointments.GroupBy(a => a.Status).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                history = world.AppointmentEvents.Count,
                leadOnDiaryDay = world.Appointments.Count(a => a.StaffUserId == world.Lead.Id && a.StartUtc is { } s
                                                               && UkTime.DateOf(s) == world.Timeline.DiaryDay
                                                               && AppointmentWorkflow.IsOnCalendar(a.Status)),
            },
            notifications = new
            {
                total = world.Notifications.Count,
                unread = world.Notifications.Count(n => n.ReadAtUtc is null),
                byKind = world.Notifications.GroupBy(n => n.Kind).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            },
        };
    }
}
