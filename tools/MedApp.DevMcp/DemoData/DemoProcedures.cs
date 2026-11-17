using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Replays every planned procedure in the order they were referred (<see cref="ProcedureReplay"/>), adds the
/// appointments that aren't for a procedure, the imported records from before the app, and saves it all in a few
/// batches.
/// </summary>
public static class DemoProcedures
{
    /// <summary>How many appointments the lead has on the diary day: a full clinic, with time left to book into.</summary>
    public const int LeadAppointmentsToday = 5;

    public static void Replay(DemoWorld world, IReadOnlyList<ProcedurePlan> plans)
    {
        var leaver = world.Staff.Single(s => s.Kind == StaffKind.Leaver);
        var leftUtc = leaver.LeftUtc!.Value;
        var oldPathwayDoneBy = world.Timeline.NowUtc.AddDays(-29);

        foreach (var plan in plans)
        {
            var lead = ChooseLead(world, plan, allowLeaver: true);
            var scale = plan.Straggler ? 3.0 : 1.0;

            for (var attempt = 0; ; attempt++)
            {
                var checkpoint = world.Allocator.Checkpoint();
                var replay = new ProcedureReplay(world, plan, lead, scale);
                replay.Run();

                // The leaver's work all closed before they left: never open work with a deleted account (H-21).
                var leaverProblem = replay.Lead.Id == leaver.Id
                                    && (!ProcedureWorkflow.IsClosed(replay.Procedure.Status) || replay.Involves(leaver, leftUtc));
                // The archived pathway's procedures finished well before the last four weeks, but for the stragglers.
                var oldPathwayProblem = plan.Pathway == Pathway.DayCase2024 && !plan.Straggler
                                        && (!ProcedureWorkflow.IsClosed(replay.Procedure.Status) || replay.LastActivityUtc > oldPathwayDoneBy);

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

    /// <summary>Who leads a procedure: the right clinician for the pathway; the leaver only for work long before they left.</summary>
    private static StaffMember ChooseLead(DemoWorld world, ProcedurePlan plan, bool allowLeaver)
    {
        var created = plan.CreatedUtc;
        var leaver = world.Staff.Single(s => s.Kind == StaffKind.Leaver);
        var leaverWeight = allowLeaver && leaver.WorksAt(created) && created < leaver.LeftUtc!.Value.AddDays(-7 * 12) ? 30 : 0;

        (StaffMember, int)[] candidates = plan.Pathway switch
        {
            Pathway.DayCase or Pathway.DayCase2024 =>
                [(world.Member("r.calloway"), 45), (world.Member("d.ashworth"), 45), (world.Lead, 12), (leaver, leaverWeight)],
            Pathway.Surgical =>
                [(world.Lead, 35), (world.Member("r.calloway"), 33), (world.Member("d.ashworth"), 32), (leaver, leaverWeight)],
            Pathway.Diagnostic => [(world.Member("m.fairbanks"), 1)],
            Pathway.Minor => [(world.Member("s.brightwell"), 88), (world.Lead, 12)],
            _ => [(world.Member("a.pemberton"), 1)],
        };

        return ProcedurePlans.Weighted(world.Random, candidates.Where(c => c.Item2 > 0).ToArray());
    }

    /// <summary>
    /// Appointments that aren't for a procedure: telephone reviews and new patient clinics, booked by the coordinator
    /// as CreateAppointmentHandler books them. One fills a nurse's removed admin time; it's booked (and so reserved)
    /// before the procedures are replayed, so nothing else takes that time.
    /// </summary>
    public static void BookFreedSlot(DemoWorld world, IReadOnlyList<PatientSeed> patients)
    {
        if (world.Calendars.FreedSlot is not { } freed)
            return;

        var nurse = world.Staff.Single(s => s.Id == freed.StaffUserId);
        var bookedAt = freed.StartUtc.AddDays(-3).AddHours(-2);
        Standalone(world, Pick(world, patients, bookedAt), nurse, freed.StartUtc, freed.EndUtc, world.Coordinator, bookedAt,
            "Telephone review: wound healing query from the patient.");
    }

    /// <summary>A few standalone appointments over the last eight weeks and the next three, and the lead's day topped up to a full clinic.</summary>
    public static void BookStandalone(DemoWorld world, IReadOnlyList<PatientSeed> patients)
    {
        var random = world.Random;
        var now = world.Timeline.NowUtc;
        string[] notes =
        [
            "New patient clinic: GP referral letter attached to the record.",
            "Telephone review of symptoms.",
            "Telephone review: results discussed.",
            "New patient clinic.",
            "Review requested by the GP.",
        ];
        var clinicians = new[] { world.Lead, world.Member("r.calloway"), world.Member("d.ashworth"), world.Member("m.fairbanks"), world.Member("k.ellison"), world.Member("s.brightwell") };

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

        // The lead's diary day: a full clinic of five, around lunch and the MDT, with free time left.
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
                var spec = DemoPathways.For(i == 0 ? Pathway.DayCase2024 : Pathway.Diagnostic);
                var kinds = ProcedureKinds.For(spec.Pathway);
                var kind = kinds[random.Next(kinds.Count)];
                var finished = patient.LastUpdatedUtc.AddDays(-random.Next(0, 40) - 60 * i);
                var created = finished.AddDays(-random.Next(40, 160));
                var id = Guid.NewGuid();
                var stages = spec.Stages.Select((s, index) =>
                {
                    var definition = world.Definitions[s.Definition];
                    var done = created.AddDays((finished - created).TotalDays * (index + 1) / spec.Stages.Length);
                    return new PatientProcedureStage
                    {
                        Id = Guid.NewGuid(),
                        PatientProcedureId = id,
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
                // migration from the single notes field leaves a procedure with no Created entry.
                const string importedNote = "Imported from the previous system.";
                var note = new PatientProcedureNote
                {
                    Id = Guid.NewGuid(),
                    PatientProcedureId = id,
                    AuthorUserId = null,
                    Text = importedNote,
                    CreatedAtUtc = created,
                    Version = 1,
                };
                world.NoteVersions.Add(new PatientProcedureNoteVersion
                {
                    Id = Guid.NewGuid(),
                    NoteId = note.Id,
                    Version = 1,
                    PatientProcedureId = id,
                    PatientId = patient.Id,
                    Text = importedNote,
                    UserId = null,
                    Username = string.Empty,
                    AtUtc = created,
                });

                world.Procedures.Add(new PatientProcedure
                {
                    Id = id,
                    PatientId = patient.Id,
                    TemplateId = null,
                    TemplateName = i == 0 ? "Imported record: day surgery" : "Imported record: diagnostics",
                    Title = kind.Title,
                    Notes = [note],
                    DueDateUtc = DueDates.EndOfDayUtc(DueDates.ToUkDate(created).AddDays(42)),
                    Priority = PatientProcedurePriority.Medium,
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
        await Batch(world.Procedures, (db, items) => db.PatientProcedures.AddRange(items), 400);
        await Batch(world.Appointments, (db, items) => db.Appointments.AddRange(items), 2000);
        await Batch(world.AppointmentEvents, (db, items) => db.AppointmentEvents.AddRange(items), 4000);
        await Batch(world.Audit, (db, items) => db.PatientProcedureAuditEntries.AddRange(items), 4000);
        await Batch(world.NoteVersions, (db, items) => db.PatientProcedureNoteVersions.AddRange(items), 4000);
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
        var procedures = world.Procedures;
        return new
        {
            total = procedures.Count,
            byStatus = procedures.GroupBy(p => p.Status).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            byTemplate = procedures.GroupBy(p => p.TemplateName).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            overdue = procedures.Count(p => ProcedureWorkflow.IsOverdue(p, now)),
            deleted = world.Deletions.Count,
            stageBypasses = world.Bypasses,
            auditEntries = world.Audit.Count,
            notes = new
            {
                total = procedures.Sum(p => p.Notes.Count),
                edited = procedures.Sum(p => p.Notes.Count(n => n.Version > 1)),
                versions = world.NoteVersions.Count,
                proceduresWithSeveral = procedures.Count(p => p.Notes.Count > 1),
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
