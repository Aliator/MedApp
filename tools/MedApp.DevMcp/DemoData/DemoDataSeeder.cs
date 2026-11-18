using System.Diagnostics;
using MedApp.DevMcp.Tools;
using MedApp.Domain.Calendar;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <param name="Seed">The random seed: the same seed gives the same data.</param>
/// <param name="NowUtc">The "now" the story ends at (the real time, unless a test fixes it).</param>
/// <param name="KeepUsers">Accounts kept through the wipe (admin always is).</param>
public sealed record DemoSeedOptions(int Seed, DateTime NowUtc, IReadOnlyCollection<string> KeepUsers);

/// <summary>
/// Wipes the local database's data and fills it with the demo centre: about 18 months of synthetic records, replayed
/// through the app's own rules (see <c>docs/demo-data.md</c> for what's in it and where to see each feature).
/// It runs with a service provider of its own (<see cref="SeedServices"/>), whose clock and activity actor it moves,
/// so the handlers it calls write history at the right moments and by the right people.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task<Dictionary<string, object?>> RunAsync(IConfiguration configuration, DemoSeedOptions options, CancellationToken ct)
    {
        // The tool checks this when it starts; checked again here, as tests and other callers come in this way.
        DevGuard.Check(configuration);

        var timer = Stopwatch.StartNew();
        var phases = new Dictionary<string, double>();
        var lap = TimeSpan.Zero;
        void Mark(string phase)
        {
            phases[phase] = Math.Round((timer.Elapsed - lap).TotalSeconds, 1);
            lap = timer.Elapsed;
        }

        var random = new Random(options.Seed);
        var timeline = new DemoTimeline(options.NowUtc);
        await using var services = new SeedServices(configuration, timeline.NowUtc);

        var keep = options.KeepUsers.Append(TestAdminTools.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await services.Run(async sp =>
        {
            var roles = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (var role in new[] { "Admin", "User" })
                if (!await roles.RoleExistsAsync(role))
                    Guards.Check(await roles.CreateAsync(new IdentityRole<Guid>(role)), $"Create role {role}");
        });

        var removed = await DemoWipe.RunAsync(services, keep, ct);
        Mark("wipe");
        await services.Run(sp => TestAdminTools.EnsureTestAdmin(
            sp.GetRequiredService<UserManager<Infrastructure.Common.Identity.ApplicationUser>>(),
            sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>(), ct));

        var staff = await DemoStaff.CreateAsync(services, timeline, ct);
        Mark("accounts");
        var (definitions, templates) = await CreatePathwayKindsAsync(services, timeline, ct);
        var calendars = await DemoCalendars.CreateAsync(services, staff, timeline, random, ct);
        var templateIds = templates.ToDictionary(t => t.Key, t => t.Value.Id);
        var rules = await DemoMonitoring.CreateAsync(services, staff, templateIds, definitions.ToDictionary(d => d.Key, d => d.Value.Id), timeline, ct);

        var plans = PathwayPlans.Create(timeline, random);
        var patients = new DemoPatients(timeline, random);
        patients.AssignPatients(plans);
        var imported = patients.AddImported();
        Mark("pathways, calendars, rules and plans");
        await patients.CreateAsync(services, staff, ct);
        Mark("patients");

        var world = new DemoWorld
        {
            Timeline = timeline,
            Random = random,
            Calendars = calendars,
            Staff = staff,
            Templates = templates,
            Definitions = definitions,
            Rules = rules,
        };

        // The lead's diary day: a full clinic, with free time left.
        calendars.Allocator.SetCap(world.Lead.Id, timeline.DiaryDay, DemoPathways.LeadAppointmentsToday);
        DemoPathways.BookFreedSlot(world, patients.All);
        DemoPathways.Replay(world, plans);
        DemoPathways.BookStandalone(world, patients.All);
        DemoPathways.AddImported(world, imported);
        // The meetings' invitations, moves and cancellations, as the app sent them, read or not like the rest.
        world.Notifications.AddRange(calendars.MeetingPlan.RecentNotifications);
        DemoPathways.MarkRead(world);
        Mark("replay");
        await DemoPathways.SaveAsync(services, world, ct);
        var pathways = DemoPathways.Summarise(world);
        Mark("save");

        var privacy = await DemoPrivacy.CreateAsync(services, world, patients, ct);
        Mark("privacy");

        object files;
        if (await DemoFiles.WhyNotAvailableAsync(configuration, ct) is { } skipped)
            files = new { skipped };
        else
            files = await DemoFiles.CreateAsync(services, world, patients.All, ct);

        Mark("files");
        var activity = await DemoActivity.CreateAsync(services, world, patients.All, ct);
        Mark("activity");
        var settings = await DemoSettings.CreateAsync(services, world, ct);
        var today = await DemoActivity.SignInTodayAsync(services, world, ct);
        Mark("settings and sign-ins");

        var manager = DemoStaff.Get(staff, DemoStaff.TwoFactorUsername);
        return new Dictionary<string, object?>
        {
            ["seed"] = options.Seed,
            ["now"] = timeline.NowUtc,
            ["seconds"] = Math.Round(timer.Elapsed.TotalSeconds, 1),
            ["secondsByPhase"] = phases,
            ["keptUsers"] = keep.Order().ToArray(),
            ["removedUsers"] = removed,
            ["testLogin"] = new { username = TestAdminTools.Username, password = TestAdminTools.Password, role = DemoStaff.Lead.JobTitle },
            ["accounts"] = staff.Where(s => s.Kind is not (StaffKind.ClinicalLead or StaffKind.Leaver)).Select(s => new
            {
                username = s.Username,
                name = s.Seed.Name,
                job = s.Seed.JobTitle,
                roles = s.Username == DemoStaff.ManagerUsername ? new[] { "Admin", "User" } : ["User"],
                password = s.Password,
                note = s.Username switch
                {
                    DemoStaff.NewStarterUsername => "New starter: must change this generated password at first sign-in. Email not confirmed yet.",
                    DemoStaff.LockedUsername => "Locked out after wrong passwords, until tomorrow evening: unlock on the Users page. No email address.",
                    DemoStaff.TwoFactorUsername => "Two-factor on: use the key below in an authenticator app, or a recovery code.",
                    DemoStaff.PasswordResetUsername => "Password reset by an admin three weeks ago, then changed by its owner.",
                    DemoStaff.NoAssignmentNotificationsUsername => "Has turned off assignment notifications.",
                    _ => null,
                },
            }).ToArray(),
            ["deletedAccount"] = DemoStaff.LeaverUsername,
            ["twoFactor"] = new { username = manager.Username, authenticatorKey = manager.TwoFactorKey, recoveryCodes = manager.RecoveryCodes },
            ["counts"] = new
            {
                staff = staff.Count,
                patients = patients.All.Count,
                importedPatients = imported.Count,
                stageDefinitions = definitions.Count,
                templates = templates.Count,
                pathways,
                calendar = new
                {
                    activityTypesAdded = calendars.ActivityTypesAdded,
                    dayTemplates = calendars.DayTemplates,
                    weekTemplates = calendars.WeekTemplates,
                    blocks = calendars.Blocks,
                    blockExceptions = calendars.Exceptions,
                    dayOverrides = calendars.Overrides,
                    meetings = calendars.Meetings.Count,
                    meetingsPast = calendars.Meetings.Count(m => m.EndUtc <= timeline.NowUtc),
                    meetingsChanged = calendars.MeetingPlan.Stories.Count(s => s.Changes > 0),
                    meetingsCancelled = calendars.MeetingPlan.Stories.Count(s => s.IsCancelled),
                    meetingNotifications = calendars.MeetingPlan.RecentNotifications.Count(),
                    meetingsAccepted = calendars.Meetings.Sum(m => m.Attendees.Count(a => a.Reply == MeetingReply.Accepted)),
                    meetingsDeclined = calendars.Meetings.Sum(m => m.Attendees.Count(a => a.Reply == MeetingReply.Declined)),
                },
                monitoringRules = rules.Count,
                activityEntries = activity,
            },
            ["privacy"] = privacy,
            ["tour"] = DemoTour.Build(world, plans, patients.All),
            ["files"] = files,
            ["settings"] = settings,
            ["today"] = today,
        };
    }

    /// <summary>The stage definitions and pathway templates, written as an admin set them up when the centre started.</summary>
    private static async Task<(Dictionary<string, PatientPathwayStageDefinition> Definitions, Dictionary<PathwayKind, PathwayTemplate> Templates)> CreatePathwayKindsAsync(
        SeedServices services, DemoTimeline timeline, CancellationToken ct)
    {
        var definitions = DemoPathwayKinds.Definitions.ToDictionary(d => d.Name, d => new PatientPathwayStageDefinition
        {
            Id = Guid.NewGuid(),
            Name = d.Name,
            Description = d.Description,
            Instructions = d.Instructions,
            SnomedCode = d.SnomedCode,
            SnomedTerm = d.SnomedTerm,
            VisitRequirement = d.Visit,
            IsArchived = d.IsArchived,
        });

        var templates = DemoPathwayKinds.PathwayKinds.ToDictionary(p => p.PathwayKind, p => new PathwayTemplate
        {
            Id = Guid.NewGuid(),
            Name = p.TemplateName,
            IsArchived = p.IsArchived,
            Maps = p.Stages.Select((s, i) => new PathwayTemplateMap
            {
                StageDefinitionId = definitions[s.Definition].Id,
                // Loaded, as the create handler reads the template: pathways are made from it (PathwayCreation).
                StageDefinition = definitions[s.Definition],
                StageOrder = i + 1,
            }).ToList(),
        });

        await services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            db.PatientPathwayStageDefinitions.AddRange(definitions.Values);
            db.PathwayTemplates.AddRange(templates.Values);
            await db.SaveChangesAsync(ct);
        });

        return (definitions, templates);
    }
}
