using System.ComponentModel;
using MediatR;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using Priority = MedApp.Domain.Procedures.PatientProcedures.PatientProcedurePriority;
using AuditAction = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;

namespace MedApp.DevMcp.Tools;

/// <summary>
/// Wipes the local database's data and fills it with a plausible, entirely synthetic demo data set:
/// staff accounts, patients, procedure stage definitions and templates, and procedures in every state.
/// Same seed, same data (timestamps are relative to "now", so the data always looks current).
/// </summary>
[McpServerToolType]
public sealed class DemoDataTools
{
    /// <summary>The "Definition missing" stage, seeded with HasData in its EF configuration and relied on by the app; never deleted.</summary>
    private static readonly Guid MissingDefinitionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [McpServerTool(Name = "reset_demo_data", Destructive = true)]
    [Description("WIPES all patients, procedures, stage definitions and templates, sessions, and every account except the kept ones (default: just the protected accounts, i.e. admin). Then it makes sure the local test login admin / admin exists, the only Admin account (see ensure_test_admin), and fills the app with plausible synthetic data: 6 staff accounts (role User), 60 patients, 11 stage definitions (one archived), 4 templates (one archived; the others repeat stages, e.g. Consultation), and about 80 procedures in every status, with stage notes and a full audit trail. admin is assigned some procedures too, so its task list isn't empty. It saves a 'pre-reset-<timestamp>' snapshot first. Needs confirm: true. The same seed gives the same data.")]
    public static async Task<object> ResetDemoData(
        IMediator mediator,
        MedAppDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        DevGuard guard,
        DevMcpOptions options,
        [Description("Must be true: this deletes data.")] bool confirm = false,
        [Description("Random seed; the default 2026 gives the standard demo data set.")] int seed = 2026,
        [Description("Extra accounts to keep, besides the protected ones. Default: none.")] string[]? keepUsers = null,
        CancellationToken ct = default)
    {
        Guards.RequireConfirm(confirm, "Resetting the database to demo data");
        var keep = options.ProtectedUsers.Concat(keepUsers ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var snapshot = $"pre-reset-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        await DatabaseTools.SaveSnapshot(guard, options, snapshot, ct);

        foreach (var role in new[] { "Admin", "User" })
            if (!await roles.RoleExistsAsync(role))
                Guards.Check(await roles.CreateAsync(new IdentityRole<Guid>(role)), $"Create role {role}");

        var removedUsers = await Wipe(db, users, keep, ct);
        await TestAdminTools.EnsureTestAdmin(users, roles, ct);
        var random = new Random(seed);
        var now = DateTime.UtcNow;

        var staff = await CreateStaff(mediator, users, ct);
        // The admin account is the only admin, so it's the one that assigned staff to the demo procedures.
        var admin = await users.FindByNameAsync(TestAdminTools.Username) ?? throw new McpException("The admin account was not created.");
        var adminStaff = new Staff(admin.UserName!, admin.Id, string.Empty);

        var patientIds = await CreatePatients(mediator, db, random, now, ct);
        var (definitionCount, templates) = await CreateStageDefinitionsAndTemplates(db, ct);
        var procedures = await CreateProcedures(db, random, now, patientIds, templates, staff, adminStaff, ct);

        return new
        {
            seed,
            snapshotBeforeReset = snapshot,
            keptUsers = keep,
            testLogin = new { username = TestAdminTools.Username, password = TestAdminTools.Password },
            removedUsers,
            staff = staff.Select(s => new { s.Username, roles = new[] { "User" }, s.Password }),
            patients = patientIds.Count,
            stageDefinitions = definitionCount,
            templates = templates.Count + 1,
            procedures,
        };
    }

    private static async Task<List<string>> Wipe(MedAppDbContext db, UserManager<ApplicationUser> users, HashSet<string> keep, CancellationToken ct)
    {
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            // Children before parents: procedure → patient and stage/map → definition don't cascade.
            await db.PatientProcedureAuditEntries.ExecuteDeleteAsync(ct);
            await db.PatientProcedureAssignments.ExecuteDeleteAsync(ct);
            await db.PatientProcedureStages.ExecuteDeleteAsync(ct);
            await db.PatientProcedures.ExecuteDeleteAsync(ct);
            await db.PatientProcedureStageTemplateItems.ExecuteDeleteAsync(ct);
            await db.PatientProcedureStageTemplates.ExecuteDeleteAsync(ct);
            await db.PatientProcedureStageDefinitions.Where(d => d.Id != MissingDefinitionId).ExecuteDeleteAsync(ct);
            await db.Patients.ExecuteDeleteAsync(ct);
            await db.UserSessions.ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        }

        var removed = new List<string>();
        foreach (var user in await users.Users.ToListAsync(ct))
        {
            if (keep.Contains(user.UserName!)) continue;
            Guards.Check(await users.DeleteAsync(user), $"Delete user {user.UserName}");
            removed.Add(user.UserName!);
        }
        return removed;
    }

    private sealed record Staff(string Username, Guid Id, string Password);

    private static async Task<List<Staff>> CreateStaff(IMediator mediator, UserManager<ApplicationUser> users, CancellationToken ct)
    {
        // Invented names; usernames follow an initial.surname pattern. All staff are role User: the admin account is
        // the only Admin.
        string[] accounts = ["a.hartwell", "k.ashford", "r.pemberton", "l.northcott", "m.calloway", "t.greenhill"];

        var created = new List<Staff>();
        foreach (var username in accounts)
        {
            var password = Passwords.Generate();
            Guards.Check(await Guards.Validated(() => mediator.Send(new CreateUserCommand(username, password), ct)), $"Create {username}");
            await Guards.Validated(() => mediator.Send(new AssignRoleCommand(username, "User"), ct));
            var user = await users.FindByNameAsync(username) ?? throw new McpException($"{username} was not created.");
            created.Add(new Staff(username, user.Id, password));
        }
        return created;
    }

    private static readonly string[] FirstNames =
    [
        "Olivia", "Amelia", "Isla", "Ava", "Mia", "Grace", "Sophia", "Freya", "Evie", "Ruby", "Ella", "Chloe", "Priya",
        "Aisha", "Zara", "Maryam", "Niamh", "Siobhan", "Margaret", "Patricia", "Susan", "Janet", "Eileen", "Joan",
        "Oliver", "George", "Harry", "Noah", "Jack", "Leo", "Arthur", "Oscar", "Muhammad", "Ibrahim", "Arjun", "Rohan",
        "Kwame", "Tomasz", "Callum", "Rhys", "Ewan", "David", "Michael", "Peter", "Brian", "Kenneth", "Graham", "Derek",
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Jones", "Taylor", "Brown", "Williams", "Wilson", "Johnson", "Davies", "Patel", "Robinson", "Wright",
        "Thompson", "Evans", "Walker", "White", "Roberts", "Green", "Hall", "Wood", "Jackson", "Clarke", "Khan", "Ahmed",
        "Hughes", "Edwards", "Turner", "Murphy", "O'Brien", "Kowalski", "Mensah", "Begum", "Ali", "Campbell", "Stewart",
        "Morgan", "Price", "Bennett", "Shaw", "Lloyd", "Harrison", "Chapman", "Kaur", "Singh", "Nowak", "Okafor", "Reid",
    ];

    private static async Task<List<Guid>> CreatePatients(IMediator mediator, MedAppDbContext db, Random random, DateTime now, CancellationToken ct)
    {
        var ids = new List<Guid>();
        var usedEmails = new HashSet<string>();
        for (var i = 0; i < 60; i++)
        {
            var first = FirstNames[random.Next(FirstNames.Length)];
            var last = LastNames[random.Next(LastNames.Length)];
            // Ages skew older, as in most procedure lists: 10% children, 25% 18-44, 35% 45-69, 30% 70-95.
            var band = random.NextDouble();
            var age = band < 0.10 ? random.Next(2, 18) : band < 0.35 ? random.Next(18, 45) : band < 0.70 ? random.Next(45, 70) : random.Next(70, 96);
            var dob = DateOnly.FromDateTime(now).AddYears(-age).AddDays(-random.Next(0, 365));

            string email;
            do email = $"{Slug(first)}.{Slug(last)}{random.Next(10, 100)}@example.com";
            while (!usedEmails.Add(email));

            var response = await Guards.Validated(() => mediator.Send(new CreatePatientCommand(first, last, dob, email), ct));
            ids.Add(response.Id);

            // Spread record history over the last 18 months instead of "all created just now".
            var createdAt = now.AddDays(-random.Next(20, 540)).AddMinutes(-random.Next(0, 1440));
            var lastUpdated = createdAt.AddDays(random.Next(0, Math.Max(1, (int)(now - createdAt).TotalDays)));
            await db.Patients.Where(p => p.Id == response.Id).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.CreatedAt, createdAt)
                .SetProperty(p => p.LastUpdated, lastUpdated), ct);
        }
        return ids;

        static string Slug(string s) => new string(s.Where(char.IsLetter).ToArray()).ToLowerInvariant();
    }

    private sealed record DefinitionSeed(Guid Id, string Name, string Description, string Instructions);

    private sealed record TemplateSeed(Guid Id, string Name, DefinitionSeed[] Stages);

    private static async Task<(int DefinitionCount, List<TemplateSeed> Templates)> CreateStageDefinitionsAndTemplates(
        MedAppDbContext db, CancellationToken ct)
    {
        var definitions = new (string Name, string Description, string Instructions)[]
        {
            ("Referral received", "The referral has been logged and triaged.", "Check the referral is complete, confirm the patient's details and set the priority."),
            ("Consultation", "The patient has been seen by the clinician.", "Review the history and examination, discuss the plan and record what was agreed."),
            ("Pre-assessment", "Fitness for the procedure has been assessed.", "Review history, medication and allergies. Arrange bloods, ECG or imaging as needed."),
            ("Consent", "Informed consent has been obtained and recorded.", "Discuss benefits, risks and alternatives. Record consent and give the patient a copy."),
            ("Scheduling", "A date, time and location have been booked.", "Book the slot, send the appointment letter and fasting or preparation instructions."),
            ("Procedure", "The procedure has been carried out.", "Complete the safety checklist and record the procedure notes."),
            ("Recovery", "Post-procedure observation is complete.", "Monitor observations, pain and wound site. Confirm the patient is fit for discharge."),
            ("Review", "The patient's recovery has been reviewed.", "Check the wound, symptoms and medication. Escalate anything unexpected."),
            ("Results review", "Results or pathology have been reviewed.", "Review reports, flag abnormal findings and agree next steps with the patient."),
            ("Follow-up", "Follow-up is arranged or the patient is discharged.", "Book a follow-up appointment or send the discharge letter to the GP."),
        };

        var byName = new Dictionary<string, DefinitionSeed>();
        foreach (var (name, description, instructions) in definitions)
        {
            var seed = new DefinitionSeed(Guid.NewGuid(), name, description, instructions);
            byName[name] = seed;
            db.PatientProcedureStageDefinitions.Add(new PatientProcedureStageDefinition
            {
                Id = seed.Id, Name = name, Description = description, Instructions = instructions,
            });
        }

        // A retired stage that no template uses any more: archived, so admins see it but can't add it to templates.
        db.PatientProcedureStageDefinitions.Add(new PatientProcedureStageDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Paper consent (retired)",
            Description = "Consent recorded on the old paper form.",
            Instructions = "No longer used: record consent with the Consent stage.",
            IsArchived = true,
        });

        // Templates list stages in order, and a stage can come back (Consultation before and after the procedure).
        var templates = new List<TemplateSeed>
        {
            new(Guid.NewGuid(), "Standard surgical pathway",
                Stages("Consultation", "Pre-assessment", "Consultation", "Consent", "Scheduling", "Procedure", "Recovery", "Review", "Consultation")),
            new(Guid.NewGuid(), "Diagnostic procedure",
                Stages("Referral received", "Consultation", "Consent", "Scheduling", "Procedure", "Results review", "Consultation")),
            new(Guid.NewGuid(), "Minor procedure (clinic)",
                Stages("Consultation", "Consent", "Procedure", "Follow-up")),
        };
        foreach (var template in templates)
            db.PatientProcedureStageTemplates.Add(new PatientProcedureStageTemplate
            {
                Id = template.Id,
                Name = template.Name,
                Maps = Maps(template.Stages),
            });

        // A template replaced by the ones above: archived, so it isn't offered for new procedures.
        db.PatientProcedureStageTemplates.Add(new PatientProcedureStageTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Day-case pathway (2024)",
            IsArchived = true,
            Maps = Maps(Stages("Referral received", "Consent", "Procedure", "Follow-up")),
        });

        await db.SaveChangesAsync(ct);
        return (definitions.Length + 1, templates);

        DefinitionSeed[] Stages(params string[] names) => names.Select(n => byName[n]).ToArray();

        static List<PatientProcedureStageTemplateMap> Maps(DefinitionSeed[] stages) =>
            stages.Select((d, i) => new PatientProcedureStageTemplateMap { StageDefinitionId = d.Id, StageOrder = i + 1 }).ToList();
    }

    private static readonly Dictionary<string, (string[] Titles, string[] Notes)> ProceduresByTemplate = new()
    {
        ["Standard surgical pathway"] = (
            ["Laparoscopic cholecystectomy", "Total knee replacement", "Total hip replacement", "Inguinal hernia repair", "Cataract surgery", "Carpal tunnel release", "Laparoscopic appendicectomy"],
            ["Day-case if pre-assessment is clear.", "Anticoagulant to be paused 5 days before; confirm with the GP.", "Patient prefers a morning list.", "Interpreter needed on the day.", "Previous anaesthetic reaction noted; anaesthetic review requested.", ""]),
        ["Diagnostic procedure"] = (
            ["Colonoscopy", "Upper GI endoscopy", "Cystoscopy", "Bronchoscopy", "Cardiac catheterisation", "Ultrasound-guided biopsy"],
            ["Bowel preparation pack posted.", "Sedation requested; patient needs an escort home.", "Two-week-wait pathway.", "Repeat of an inconclusive previous test.", ""]),
        ["Minor procedure (clinic)"] = (
            ["Skin lesion excision", "Joint injection", "Ingrown toenail removal", "Punch biopsy", "Sebaceous cyst removal"],
            ["Photograph the lesion before excision.", "Steroid injection; check the date of the last injection.", "Can be seen in the nurse-led clinic.", ""]),
    };

    // Synthetic stage notes, as staff might write them.
    private static readonly Dictionary<string, string[]> StageNotes = new()
    {
        ["Referral received"] = ["Referral complete; triaged as routine.", "GP letter attached; priority confirmed with the consultant."],
        ["Consultation"] = ["Discussed the options; patient keen to go ahead.", "Reviewed progress with the patient. No concerns raised.", "Explained the next steps and gave the patient a leaflet."],
        ["Pre-assessment"] = ["Bloods and ECG normal. Fit for the procedure.", "Medication reviewed; anticoagulant plan agreed with the GP."],
        ["Consent"] = ["Risks, benefits and alternatives discussed. Consent form signed.", "Consent recorded; copy given to the patient."],
        ["Scheduling"] = ["Booked on the morning list; letter sent.", "Date agreed by phone; preparation instructions posted."],
        ["Procedure"] = ["Procedure uneventful. Full notes on the operation record.", "Completed as planned; no complications."],
        ["Recovery"] = ["Observations stable; discharged home with an escort.", "Pain controlled; wound clean and dry."],
        ["Review"] = ["Wound healing well. Sutures removed.", "Recovering as expected; no further concerns."],
        ["Results review"] = ["Results normal; discussed with the patient.", "Pathology reviewed; findings explained to the patient."],
        ["Follow-up"] = ["Discharged back to the GP.", "Follow-up booked in six weeks."],
    };

    private static readonly string[] BlockReasons =
        ["Waiting for blood results from the GP.", "Patient unwell; procedure on hold until they recover.", "Awaiting an interpreter booking."];

    private static readonly string[] CancelReasons =
        ["Patient chose not to go ahead.", "Symptoms resolved; procedure no longer needed.", "Duplicate referral."];

    private static async Task<object> CreateProcedures(
        MedAppDbContext db, Random random, DateTime now, List<Guid> patientIds, List<TemplateSeed> templates,
        List<Staff> clinicians, Staff admin, CancellationToken ct)
    {
        // Status mix of a live service: lots in flight, a good share finished, a few stuck or stopped.
        var statusWeights = new (Status Status, int Weight)[]
        {
            (Status.Unassigned, 10), (Status.Assigned, 14), (Status.InProgress, 28), (Status.InReview, 8),
            (Status.Completed, 22), (Status.Blocked, 6), (Status.Cancelled, 5), (Status.Archived, 3),
        };
        var priorityWeights = new (Priority Priority, int Weight)[] { (Priority.Low, 25), (Priority.Medium, 40), (Priority.High, 25), (Priority.Urgent, 10) };
        var today = DueDates.ToUkDate(now);
        var auditEntries = 0;

        // About 80 procedures: most patients have one or two, some none, a few three.
        var count = 0;
        foreach (var patientId in patientIds)
        {
            var roll = random.NextDouble();
            var perPatient = roll < 0.12 ? 0 : roll < 0.62 ? 1 : roll < 0.90 ? 2 : 3;
            for (var i = 0; i < perPatient; i++, count++)
            {
                var template = templates[random.Next(templates.Count)];
                var stages = template.Stages;
                var (titles, notes) = ProceduresByTemplate[template.Name];
                var status = Pick(random, statusWeights);
                var createdAt = now.AddDays(-random.Next(3, 300)).AddMinutes(-random.Next(0, 1440));
                var closed = status is Status.Completed or Status.Archived or Status.Cancelled;

                // Stages are finished in order; finishing the last one sends the procedure to review.
                var finishedStages = status switch
                {
                    Status.Unassigned or Status.Assigned => 0,
                    Status.InProgress => random.Next(0, stages.Length - 1),
                    Status.Blocked => random.Next(1, stages.Length - 1),
                    Status.Cancelled => random.Next(0, 3),
                    _ => stages.Length,
                };

                // Open work is due 1-16 weeks out; about 1 in 6 open items is overdue.
                var dueDate = closed
                    ? DueDates.ToUkDate(createdAt.AddDays(random.Next(14, 90)))
                    : random.NextDouble() < 0.17
                        ? today.AddDays(-random.Next(1, 21))
                        : today.AddDays(random.Next(3, 112));

                var procedure = new PatientProcedure
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    Title = titles[random.Next(titles.Length)],
                    Notes = notes[random.Next(notes.Length)],
                    Priority = Pick(random, priorityWeights),
                    Status = Status.Unassigned,
                    DueDateUtc = DueDates.EndOfDayUtc(dueDate),
                    CreatedAt = createdAt,
                    Stages = stages.Select((d, index) => new PatientProcedureStage
                    {
                        Id = Guid.NewGuid(),
                        StageDefinitionId = d.Id,
                        StageOrder = index + 1,
                        Name = d.Name,
                        Description = d.Description,
                        Instructions = d.Instructions,
                        CreatedAt = createdAt,
                        LastUpdated = createdAt,
                    }).ToList(),
                };

                // The history is replayed through ProcedureChange, as the app records it, so the audit log matches.
                var cursor = createdAt;
                DateTime Next(int minMinutes, int maxMinutes)
                {
                    var next = cursor.AddMinutes(random.Next(minMinutes, maxMinutes));
                    cursor = next < now ? next : cursor.AddMinutes(1);
                    return cursor;
                }
                var audit = new List<PatientProcedureAuditEntry>();
                ProcedureChange As(Staff who, DateTime at) => new(procedure, new ProcedureActor(who.Id, who.Username), at);
                void Record(ProcedureChange change) => audit.AddRange(change.AuditEntries);

                var creator = clinicians[random.Next(clinicians.Count)];
                var created = As(creator, createdAt);
                created.Audit(AuditAction.Created, new { template = template.Name, dueDate, priority = procedure.Priority.ToString(), stageCount = stages.Length });
                Record(created);

                var assignees = new List<Staff>();
                if (status != Status.Unassigned)
                {
                    assignees.AddRange(clinicians.OrderBy(_ => random.Next()).Take(random.NextDouble() < 0.25 ? 2 : 1));
                    if (random.NextDouble() < 0.2) assignees.Add(admin);
                    foreach (var assignee in assignees)
                    {
                        var change = As(admin, Next(30, 2 * 24 * 60));
                        change.Assign(assignee.Id, assignee.Username);
                        Record(change);
                    }
                }

                // Any staff member can work on a procedure, but it's usually one of its assignees.
                Staff Worker() => assignees.Count > 0 && random.NextDouble() < 0.85
                    ? assignees[random.Next(assignees.Count)]
                    : clinicians[random.Next(clinicians.Count)];

                var spacing = (int)Math.Max(120, (now - createdAt).TotalMinutes / (stages.Length + 2));
                var ordered = procedure.Stages.OrderBy(s => s.StageOrder).ToList();
                for (var s = 0; s < finishedStages; s++)
                {
                    var worker = Worker();
                    var stage = ordered[s];
                    var noteOptions = StageNotes[stage.Name];
                    var start = As(worker, Next(60, spacing));
                    start.StartStage(stage);
                    Record(start);
                    var write = As(worker, Next(5, 90));
                    write.UpdateStageNotes(stage, noteOptions[random.Next(noteOptions.Length)]);
                    Record(write);
                    var finish = As(worker, Next(1, 30));
                    finish.FinishStage(stage);
                    Record(finish);
                }

                // Work under way on the current stage, sometimes with notes started.
                if (status is Status.InProgress or Status.Blocked && finishedStages < ordered.Count)
                {
                    var worker = Worker();
                    var stage = ordered[finishedStages];
                    var start = As(worker, Next(60, spacing));
                    start.StartStage(stage);
                    Record(start);
                    if (random.NextDouble() < 0.5)
                    {
                        var write = As(worker, Next(5, 90));
                        write.UpdateStageNotes(stage, "In progress: " + StageNotes[stage.Name][0]);
                        Record(write);
                    }
                }

                // Now and then an admin has reopened a finished stage to correct it.
                if (status == Status.InProgress && finishedStages > 0 && random.NextDouble() < 0.15)
                {
                    var unlock = As(admin, Next(30, 600));
                    unlock.UnlockStage(ordered[finishedStages - 1]);
                    Record(unlock);
                }

                switch (status)
                {
                    case Status.Blocked:
                    {
                        var change = As(Worker(), Next(30, 600));
                        change.ChangeStatus(Status.Blocked, BlockReasons[random.Next(BlockReasons.Length)]);
                        Record(change);
                        break;
                    }
                    case Status.Cancelled:
                    {
                        var change = As(Worker(), Next(30, 600));
                        change.ChangeStatus(Status.Cancelled, CancelReasons[random.Next(CancelReasons.Length)]);
                        Record(change);
                        break;
                    }
                    case Status.Completed or Status.Archived:
                    {
                        var change = As(Worker(), Next(60, 2 * 24 * 60));
                        change.ChangeStatus(Status.Completed, null);
                        Record(change);
                        if (status == Status.Archived)
                        {
                            var archive = As(Worker(), Next(24 * 60, 14 * 24 * 60));
                            archive.ChangeStatus(Status.Archived, null);
                            Record(archive);
                        }
                        break;
                    }
                }

                if (procedure.Status != status)
                    throw new McpException($"Seeding went wrong: a procedure meant to be {status} ended up {procedure.Status}.");

                procedure.LastUpdated = audit.Max(a => a.AtUtc);
                db.PatientProcedures.Add(procedure);
                db.PatientProcedureAuditEntries.AddRange(audit);
                auditEntries += audit.Count;
            }
        }
        await db.SaveChangesAsync(ct);

        var byStatus = await db.PatientProcedures.GroupBy(p => p.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var open = ProcedureWorkflow.OpenStatuses.ToList();
        var overdue = await db.PatientProcedures.CountAsync(p => p.DueDateUtc < now && open.Contains(p.Status), ct);
        var assignedToAdmin = await db.PatientProcedureAssignments.CountAsync(a => a.UserId == admin.Id, ct);
        return new
        {
            total = count,
            overdue,
            assignedToAdmin,
            auditEntries,
            byStatus = byStatus.OrderBy(x => x.Key).ToDictionary(x => x.Key.ToString(), x => x.Count),
        };
    }

    private static T Pick<T>(Random random, (T Item, int Weight)[] weights)
    {
        var roll = random.Next(weights.Sum(w => w.Weight));
        foreach (var (item, weight) in weights)
        {
            if (roll < weight) return item;
            roll -= weight;
        }
        return weights[^1].Item;
    }
}
