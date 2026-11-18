using System.Diagnostics;
using MedApp.Application.Common.Identity;
using MedApp.DevMcp.Tools;
using MedApp.Domain.Calendar;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Common.Identity;
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
        // Whether files can be stored: without a store, the files the app's commands make are dropped (their records stay).
        var filesSkipped = await DemoFiles.WhyNotAvailableAsync(configuration, ct);
        await using var services = new SeedServices(configuration, timeline.NowUtc, discardFiles: filesSkipped is not null);

        var keep = options.KeepUsers.Append(TestAdminTools.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The standard roles, with their permissions put back as they start (an admin may have edited them locally).
        await services.Run(sp => StandardRoleSetup.EnsureAsync(
            sp.GetRequiredService<MedAppDbContext>(), timeline.NowUtc, resetPermissions: true, ct));

        var removed = await DemoWipe.RunAsync(services, keep, ct);
        Mark("wipe");
        await services.Run(sp => TestAdminTools.EnsureTestAdmin(
            sp.GetRequiredService<UserManager<ApplicationUser>>(),
            sp.GetRequiredService<MedAppDbContext>(), ct));

        // The clinical lead holds exactly Admin and Clinician in the demo (a kept account may have had others).
        await services.Run(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await users.FindByNameAsync(TestAdminTools.Username) ?? throw new InvalidOperationException("No admin account.");
            var extra = (await users.GetRolesAsync(admin)).Except(TestAdminTools.Roles, StringComparer.OrdinalIgnoreCase).ToList();
            if (extra.Count > 0)
                Guards.Check(await users.RemoveFromRolesAsync(admin, extra), "Set the admin account's roles");
        });

        var staff = await DemoStaff.CreateAsync(services, timeline, ct);
        var specialties = await DemoSpecialties.CreateAsync(services, staff[0], timeline, ct);
        var memberships = await DemoSpecialties.AddMembershipsAsync(services, staff, specialties, ct);
        var specialtyTargets = await DemoSpecialties.LoadAsync(services, ct);
        Mark("accounts");
        var (definitions, templates) = await CreatePathwayKindsAsync(services, specialties.Ids, ct);
        var calendars = await DemoCalendars.CreateAsync(services, staff, timeline, random, specialties.Ids[DemoSpecialties.Dermatology], ct);
        var templateIds = templates.ToDictionary(t => t.Key, t => t.Value.Id);
        var rules = await DemoMonitoring.CreateAsync(services, staff, templateIds, definitions.ToDictionary(d => d.Key, d => d.Value.Id), timeline, ct);
        var specimenSetup = await DemoSpecimens.CreateSetupAsync(services, timeline, ct);
        var setup = await DemoCentreSetup.CreateAsync(services, staff, timeline, ct);

        var plans = PathwayPlans.Create(timeline, random);
        var patients = new DemoPatients(timeline, random);
        patients.AssignPatients(plans);
        // The referrals that never became pathways, and the inbox now: their patients are registered with the rest.
        var stories = ReferralStories.Plan(plans, patients, timeline, random);
        var imported = patients.AddImported();
        Mark("pathways, calendars, rules and plans");
        await patients.CreateAsync(services, staff, setup, ct);
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
            Specialties = specialtyTargets,
            Setup = setup,
            SpecimenSetup = specimenSetup,
            MdtMeetings = calendars.Meetings.Where(m => m.IsMdt).OrderBy(m => m.StartUtc).ToList(),
        };

        // The lead's diary day: a full clinic, with free time left.
        calendars.Allocator.SetCap(world.Lead.Id, timeline.DiaryDay, DemoPathways.LeadAppointmentsToday);
        DemoPathways.BookFreedSlot(world, patients.All);
        DemoPathways.Replay(world, plans);
        DemoPathways.BookStandalone(world, patients.All);
        DemoPathways.AddImported(world, imported);
        // The referrals that weren't accepted, and those awaiting triage now, after the pathways' (whose referrals the
        // duplicate checks see).
        var played = ReferralStories.Play(world, stories, patients.All);
        // The meetings' invitations, moves and cancellations, as the app sent them, read or not like the rest.
        world.Notifications.AddRange(calendars.MeetingPlan.RecentNotifications);
        DemoPathways.MarkRead(world);
        Mark("replay");
        DemoLab.AddLeaversResult(world, patients.All);
        await DemoPathways.SaveAsync(services, world, ct);
        await DemoReferrals.SaveAsync(services, world, ct);
        await DemoLab.SaveAsync(services, world, ct);
        var pathways = DemoPathways.Summarise(world);
        var referrals = DemoReferrals.Summarise(world, played);
        var lab = DemoLab.Summarise(world);
        Mark("save");

        // Letters through the letter commands, before any record is restricted (which stops letters).
        var letters = await DemoLetters.CreateAsync(services, world, ct);
        var clinics = await DemoClinics.CreateAsync(services, world, patients.All, ct);
        Mark("letters and clinics");

        var privacy = await DemoPrivacy.CreateAsync(services, world, patients, ct);
        var reminders = await DemoReminders.CreateAsync(services, world, ct);
        var dataRequestFor = await DemoDataRequests.CreateAsync(services, world, patients.All, ct);
        Mark("privacy and reminders");

        object files;
        if (filesSkipped is { } skipped)
            files = new { skipped, reportsWithoutTheirBytes = world.Attachments.Count };
        else
            files = new { uploaded = await DemoFiles.CreateAsync(services, world, patients.All, ct), reports = await DemoLab.UploadReportsAsync(services, world, ct) };

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
                roles = s.Username == DemoStaff.ManagerUsername ? new[] { AppRoles.Admin, s.RoleName } : [s.RoleName],
                specialties = DemoSpecialties.For(s.Kind),
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
                specialties = specialties.Ids.Count,
                specialtyMemberships = memberships,
                pathways,
                referrals,
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
                specimenTypes = specimenSetup.Types.Count,
                laboratories = specimenSetup.Labs.Count,
                practices = setup.Practices.Count,
                rooms = setup.Rooms.Count,
                sitesWithHours = setup.SitesWithHours,
                staffWithHours = setup.StaffWithHours,
                letterTemplates = setup.LetterTemplates.Count,
                lab,
                letters = letters.Counts,
                clinics = clinics.Counts,
                reminders,
                activityEntries = activity,
            },
            ["privacy"] = privacy,
            ["openDataRequestFor"] = dataRequestFor,
            ["tour"] = DemoTour.Build(world, plans, patients.All, stories, letters.Records, clinics.Tour),
            ["files"] = files,
            ["settings"] = settings,
            ["today"] = today,
        };
    }

    /// <summary>The stage definitions and pathway templates, written as an admin set them up when the centre started.</summary>
    private static async Task<(Dictionary<string, PatientPathwayStageDefinition> Definitions, Dictionary<PathwayKind, PathwayTemplate> Templates)> CreatePathwayKindsAsync(
        SeedServices services, IReadOnlyDictionary<string, Guid> specialties, CancellationToken ct)
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
            // What a stage needs before it's finished (Phases 3 to 5): copied onto pathways created from then on.
            WaitsForResults = d.WaitsForResults,
            NeedsLetterKind = d.NeedsLetter,
            NeedsConsent = d.NeedsConsent,
        });

        var templates = DemoPathwayKinds.PathwayKinds.ToDictionary(p => p.PathwayKind, p => new PathwayTemplate
        {
            Id = Guid.NewGuid(),
            Name = p.TemplateName,
            IsArchived = p.IsArchived,
            SpecialtyId = specialties[DemoSpecialties.For(p.PathwayKind)],
            // Skin lesions may be cancers: their records are kept on the oncology period.
            RetentionClass = p.PathwayKind is PathwayKind.SkinLesion or PathwayKind.SkinLesion2024
                ? RetentionClass.Oncology
                : RetentionClass.Standard,
            Maps = p.Stages.Select((s, i) => new PathwayTemplateMap
            {
                StageDefinitionId = definitions[s.Definition].Id,
                // Loaded, as the create handler reads the template: pathways are made from it (PathwayCreation).
                StageDefinition = definitions[s.Definition],
                StageOrder = i + 1,
                // "The waiting-time clock stops when this stage is finished": one per template.
                StopsClock = i == p.ClockStopIndex,
                IsOptional = s.Optional,
                ChoosesBranch = s.ChoosesBranch,
                Branch = s.Branch,
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
