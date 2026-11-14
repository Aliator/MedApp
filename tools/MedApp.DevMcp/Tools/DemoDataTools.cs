using System.ComponentModel;
using MediatR;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using Priority = MedApp.Domain.Procedures.PatientProcedures.PatientProcedurePriority;

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
    [Description("WIPES all patients, procedures, stage definitions and templates, sessions, and every account except the kept ones (default: the protected accounts plus parity-admin). Then it fills the app with plausible synthetic data: 6 staff accounts, 60 patients, 8 stage definitions, 3 templates and about 80 procedures in every status. It saves a 'pre-reset-<timestamp>' snapshot first. Needs confirm: true. The same seed gives the same data.")]
    public static async Task<object> ResetDemoData(
        IMediator mediator,
        MedAppDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        DevGuard guard,
        DevMcpOptions options,
        [Description("Must be true: this deletes data.")] bool confirm = false,
        [Description("Random seed; the default 2026 gives the standard demo data set.")] int seed = 2026,
        [Description("Extra accounts to keep, besides the protected ones. Default: [\"parity-admin\"], used by the UI screenshot tool.")] string[]? keepUsers = null,
        CancellationToken ct = default)
    {
        Guards.RequireConfirm(confirm, "Resetting the database to demo data");
        var keep = options.ProtectedUsers.Concat(keepUsers ?? ["parity-admin"]).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var snapshot = $"pre-reset-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        await DatabaseTools.SaveSnapshot(guard, options, snapshot, ct);

        foreach (var role in new[] { "Admin", "User" })
            if (!await roles.RoleExistsAsync(role))
                Guards.Check(await roles.CreateAsync(new IdentityRole<Guid>(role)), $"Create role {role}");

        var removedUsers = await Wipe(db, users, keep, ct);
        var random = new Random(seed);
        var now = DateTime.UtcNow;

        var staff = await CreateStaff(mediator, users, ct);
        var assigners = staff.Where(s => s.IsAdmin).Select(s => s.Id).ToList();
        var clinicians = staff.Select(s => s.Id).ToList();

        var patientIds = await CreatePatients(mediator, db, random, now, ct);
        var (definitions, templates) = await CreateStageDefinitionsAndTemplates(db, ct);
        var procedures = await CreateProcedures(db, random, now, patientIds, definitions, templates, clinicians, assigners, ct);

        return new
        {
            seed,
            snapshotBeforeReset = snapshot,
            keptUsers = keep,
            removedUsers,
            staff = staff.Select(s => new { s.Username, roles = s.IsAdmin ? new[] { "Admin", "User" } : ["User"], s.Password }),
            patients = patientIds.Count,
            stageDefinitions = definitions.Count,
            templates = templates.Count,
            procedures,
        };
    }

    private static async Task<List<string>> Wipe(MedAppDbContext db, UserManager<ApplicationUser> users, HashSet<string> keep, CancellationToken ct)
    {
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            // Children before parents: procedure → patient and stage/map → definition don't cascade.
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

    private sealed record Staff(string Username, Guid Id, bool IsAdmin, string Password);

    private static async Task<List<Staff>> CreateStaff(IMediator mediator, UserManager<ApplicationUser> users, CancellationToken ct)
    {
        // Invented names; usernames follow an initial.surname pattern.
        var accounts = new (string Username, bool IsAdmin)[]
        {
            ("a.hartwell", true), ("k.ashford", true), ("r.pemberton", false),
            ("l.northcott", false), ("m.calloway", false), ("t.greenhill", false),
        };

        var created = new List<Staff>();
        foreach (var (username, isAdmin) in accounts)
        {
            var password = Passwords.Generate();
            Guards.Check(await Guards.Validated(() => mediator.Send(new CreateUserCommand(username, password), ct)), $"Create {username}");
            foreach (var role in isAdmin ? new[] { "Admin", "User" } : ["User"])
                await Guards.Validated(() => mediator.Send(new AssignRoleCommand(username, role), ct));
            var user = await users.FindByNameAsync(username) ?? throw new McpException($"{username} was not created.");
            created.Add(new Staff(username, user.Id, isAdmin, password));
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

    private static async Task<(Dictionary<string, Guid> Definitions, List<(string Name, Guid[] Stages)> Templates)>
        CreateStageDefinitionsAndTemplates(MedAppDbContext db, CancellationToken ct)
    {
        var definitions = new (string Name, string Description, string Instructions)[]
        {
            ("Referral received", "The referral has been logged and triaged.", "Check the referral is complete, confirm the patient's details and set the priority."),
            ("Pre-assessment", "Fitness for the procedure has been assessed.", "Review history, medication and allergies. Arrange bloods, ECG or imaging as needed."),
            ("Consent", "Informed consent has been obtained and recorded.", "Discuss benefits, risks and alternatives. Record consent and give the patient a copy."),
            ("Scheduling", "A date, time and location have been booked.", "Book the slot, send the appointment letter and fasting or preparation instructions."),
            ("Procedure", "The procedure has been carried out.", "Complete the safety checklist and record the procedure notes."),
            ("Recovery", "Post-procedure observation is complete.", "Monitor observations, pain and wound site. Confirm the patient is fit for discharge."),
            ("Results review", "Results or pathology have been reviewed.", "Review reports, flag abnormal findings and agree next steps with the patient."),
            ("Follow-up", "Follow-up is arranged or the patient is discharged.", "Book a follow-up appointment or send the discharge letter to the GP."),
        };

        var ids = new Dictionary<string, Guid>();
        foreach (var (name, description, instructions) in definitions)
        {
            var definition = new PatientProcedureStageDefinition { Id = Guid.NewGuid(), Name = name, Description = description, Instructions = instructions };
            db.PatientProcedureStageDefinitions.Add(definition);
            ids[name] = definition.Id;
        }

        var templates = new List<(string Name, Guid[] Stages)>
        {
            ("Standard surgical pathway", Stages("Referral received", "Pre-assessment", "Consent", "Scheduling", "Procedure", "Recovery", "Follow-up")),
            ("Diagnostic procedure", Stages("Referral received", "Consent", "Scheduling", "Procedure", "Results review", "Follow-up")),
            ("Minor procedure (clinic)", Stages("Referral received", "Consent", "Procedure", "Follow-up")),
        };
        foreach (var (name, stages) in templates)
        {
            db.PatientProcedureStageTemplates.Add(new PatientProcedureStageTemplate
            {
                Id = Guid.NewGuid(),
                Name = name,
                Maps = stages.Select((id, i) => new PatientProcedureStageTemplateMap { StageDefinitionId = id, StageOrder = i + 1 }).ToList(),
            });
        }
        await db.SaveChangesAsync(ct);
        return (ids, templates);

        Guid[] Stages(params string[] names) => names.Select(n => ids[n]).ToArray();
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

    private static async Task<object> CreateProcedures(
        MedAppDbContext db, Random random, DateTime now, List<Guid> patientIds,
        Dictionary<string, Guid> definitions, List<(string Name, Guid[] Stages)> templates,
        List<Guid> clinicians, List<Guid> assigners, CancellationToken ct)
    {
        // Status mix of a live service: lots in flight, a good share finished, a few stuck or stopped.
        var statusWeights = new (Status Status, int Weight)[]
        {
            (Status.Unassigned, 8), (Status.Assigned, 8), (Status.NotStarted, 8), (Status.InProgress, 26),
            (Status.InReview, 8), (Status.Completed, 22), (Status.Blocked, 6), (Status.Cancelled, 5), (Status.Archived, 3),
        };
        var priorityWeights = new (Priority Priority, int Weight)[] { (Priority.Low, 25), (Priority.Medium, 40), (Priority.High, 25), (Priority.Urgent, 10) };

        // About 80 procedures: most patients have one or two, some none, a few three.
        var count = 0;
        foreach (var patientId in patientIds)
        {
            var roll = random.NextDouble();
            var perPatient = roll < 0.12 ? 0 : roll < 0.62 ? 1 : roll < 0.90 ? 2 : 3;
            for (var i = 0; i < perPatient; i++, count++)
            {
                var (templateName, stages) = templates[random.Next(templates.Count)];
                var (titles, notes) = ProceduresByTemplate[templateName];
                var status = Pick(random, statusWeights);
                var createdAt = now.AddDays(-random.Next(3, 300)).AddMinutes(-random.Next(0, 1440));

                var completedStages = status switch
                {
                    Status.Unassigned or Status.Assigned or Status.NotStarted => 0,
                    Status.InProgress => random.Next(1, stages.Length - 1),
                    Status.InReview => stages.Length - 1,
                    Status.Completed or Status.Archived => stages.Length,
                    Status.Blocked => random.Next(1, stages.Length - 1),
                    Status.Cancelled => random.Next(0, 2),
                    _ => 0,
                };
                var finished = status is Status.Completed or Status.Archived or Status.Cancelled;

                // Open work is due 1-16 weeks after creation; about 1 in 6 open items is overdue.
                var dueDate = finished
                    ? createdAt.AddDays(random.Next(14, 90))
                    : random.NextDouble() < 0.17
                        ? now.AddDays(-random.Next(1, 21))
                        : now.AddDays(random.Next(3, 112));
                dueDate = dueDate.Date.AddHours(9);

                var procedure = new PatientProcedure
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    Title = titles[random.Next(titles.Length)],
                    Notes = notes[random.Next(notes.Length)],
                    Priority = Pick(random, priorityWeights),
                    Status = status,
                    DueDateUtc = dueDate,
                    CreatedAt = createdAt,
                };

                var assignees = status == Status.Unassigned
                    ? []
                    : clinicians.OrderBy(_ => random.Next()).Take(random.NextDouble() < 0.25 ? 2 : 1).ToList();
                foreach (var userId in assignees)
                {
                    procedure.Assignments.Add(new PatientProcedureAssignment
                    {
                        UserId = userId,
                        AssignedByUserId = assigners[random.Next(assigners.Count)],
                        AssignedAtUtc = createdAt.AddHours(random.Next(1, 72)),
                    });
                }

                // Completed stages happen in order between creation and now.
                var cursor = createdAt;
                var span = (now - createdAt).TotalMinutes;
                for (var s = 0; s < stages.Length; s++)
                {
                    var done = s < completedStages;
                    if (done) cursor = cursor.AddMinutes(random.Next(60, (int)Math.Max(120, span / (stages.Length + 1))));
                    procedure.Stages.Add(new PatientProcedureStage
                    {
                        Id = Guid.NewGuid(),
                        StageDefinitionId = stages[s],
                        StageOrder = s + 1,
                        IsCompleted = done,
                        CompletedAtUtc = done ? cursor : null,
                        CompletedByUserId = done && assignees.Count > 0 ? assignees[random.Next(assignees.Count)] : null,
                        CreatedAt = createdAt,
                        LastUpdated = done ? cursor : createdAt,
                    });
                }
                procedure.LastUpdated = completedStages > 0 ? cursor : createdAt.AddHours(random.Next(1, 48));
                if (procedure.LastUpdated > now) procedure.LastUpdated = now;
                db.PatientProcedures.Add(procedure);
            }
        }
        await db.SaveChangesAsync(ct);

        var byStatus = await db.PatientProcedures.GroupBy(p => p.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var overdue = await db.PatientProcedures.CountAsync(p => p.DueDateUtc < now && p.Status != Status.Completed && p.Status != Status.Cancelled && p.Status != Status.Archived, ct);
        return new { total = count, overdue, byStatus = byStatus.OrderBy(x => x.Key).ToDictionary(x => x.Key.ToString(), x => x.Count) };
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
