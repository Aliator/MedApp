using System.Diagnostics;
using MedApp.Application.Common.Identity;
using MedApp.DevMcp.Tools;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <param name="Seed">The random seed (kept for the tool's contract: the demo is the same story each run).</param>
/// <param name="NowUtc">The "now" the story ends at (the real time, unless a test fixes it).</param>
/// <param name="KeepUsers">Accounts kept through the wipe (admin always is).</param>
public sealed record DemoSeedOptions(int Seed, DateTime NowUtc, IReadOnlyCollection<string> KeepUsers);

/// <summary>
/// Wipes the local database's data and fills it with the light demo's clinic (<c>MedApp.Web/src/demo</c>): Dermatology
/// and the lab, the same people, templates, patients and pathways at the same points, replayed through the app's own
/// handlers by the people who did the work (staff in their teams; the admin only sets up), so the audit trails,
/// notifications, grants and gates are the app's. Dates are relative to the day it runs, as the light demo's are. It runs
/// with a service provider of its own (<see cref="SeedServices"/>), whose clock and activity actor it moves. What's in it:
/// <c>docs/demo-data.md</c>.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task<Dictionary<string, object?>> RunAsync(IConfiguration configuration, DemoSeedOptions options, CancellationToken ct)
    {
        // The tool checks this when it starts; checked again here, as tests and other callers come in this way.
        DevGuard.Check(configuration);

        var timer = Stopwatch.StartNew();
        var timeline = new DemoTimeline(options.NowUtc);
        // Whether files can be stored: without a store, the files the app's commands make are dropped (their records stay).
        var filesSkipped = await DemoFiles.WhyNotAvailableAsync(configuration, ct);
        await using var services = new SeedServices(configuration, timeline.NowUtc, discardFiles: filesSkipped is not null);

        var keep = options.KeepUsers.Append(TestAdminTools.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The standard roles, with their permissions put back as they start (an admin may have edited them locally).
        await services.Run(sp => StandardRoleSetup.EnsureAsync(
            sp.GetRequiredService<MedAppDbContext>(), timeline.NowUtc, resetPermissions: true, ct));

        var removed = await DemoWipe.RunAsync(services, keep, ct);
        await services.Run(sp => TestAdminTools.EnsureTestAdmin(
            sp.GetRequiredService<UserManager<ApplicationUser>>(),
            sp.GetRequiredService<MedAppDbContext>(), ct));

        var seed = new DemoSeed(services, timeline, DemoPeople.Roster(), ct);
        await DemoPeople.SetUpAdminAsync(seed);
        await DemoPeople.CreateAccountsAsync(seed);
        await DemoSetup.CreateAsync(seed);
        await DemoPatients.CreateAsync(seed);
        var (mdtToday, mdtAhead) = await DemoClinic.CreateMeetingsAsync(seed);
        await DemoStory.RunAsync(seed);
        await DemoLab.CreateAsync(seed);
        await DemoClinic.CreateMdtListsAsync(seed, mdtToday, mdtAhead);
        await DemoSetup.CreateCalendarSetupAsync(seed);
        await DemoClinic.CreateBlocksAsync(seed, mdtToday, mdtAhead);
        var activity = await DemoActivity.CreateAsync(seed);
        await DemoClinic.FinishAsync(seed);

        var counts = await services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            return new
            {
                staff = seed.People.Count,
                patients = await db.Patients.CountAsync(ct),
                teams = await db.Teams.CountAsync(t => !t.IsArchived, ct),
                stageDefinitions = await db.PatientPathwayStageDefinitions.CountAsync(ct) - 1,
                templates = await db.PathwayTemplates.CountAsync(ct),
                pathways = await db.PatientPathways.CountAsync(ct),
                referrals = await db.Referrals.CountAsync(ct),
                appointments = await db.Appointments.CountAsync(ct),
                specimens = await db.Specimens.CountAsync(ct),
                labCases = await db.LabCases.CountAsync(ct),
                results = await db.Results.CountAsync(ct),
                hospitalRequests = await db.HospitalRequests.CountAsync(ct),
                letters = await db.Letters.CountAsync(ct),
                consentRecords = await db.ConsentRecords.CountAsync(ct),
                mdtItems = await db.MdtItems.CountAsync(ct),
                consults = await db.Consults.CountAsync(ct),
                meetings = await db.Meetings.CountAsync(ct),
                calendarBlocks = await db.CalendarBlocks.CountAsync(ct),
                clinicSessions = await db.ClinicSessions.CountAsync(ct),
                notifications = await db.Notifications.CountAsync(ct),
                activityEntries = await db.ActivityEntries.CountAsync(ct),
                seededActivityEntries = activity,
            };
        });

        return new Dictionary<string, object?>
        {
            ["seed"] = options.Seed,
            ["now"] = timeline.NowUtc,
            ["seconds"] = Math.Round(timer.Elapsed.TotalSeconds, 1),
            ["keptUsers"] = keep.Order().ToArray(),
            ["removedUsers"] = removed,
            ["testLogin"] = new { username = TestAdminTools.Username, password = TestAdminTools.Password, role = AppRoles.Admin },
            ["accounts"] = seed.People.Where(p => !p.IsAdmin).Select(p => new
            {
                username = p.Username,
                name = p.FullName,
                job = p.JobTitle,
                roles = p.Roles.Select(r => r.Name).ToArray(),
                teams = p.Teams.Select(x => x.Role == Domain.Teams.TeamMemberRole.ClinicAdmin ? $"{x.Team} (clinic admin)" : x.Team).ToArray(),
                password = p.Password,
            }).ToArray(),
            ["counts"] = counts,
            ["more"] = seed.Counts,
            ["tour"] = seed.Tour,
            ["filesSkipped"] = filesSkipped,
        };
    }
}
