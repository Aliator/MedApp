using System.Diagnostics;
using MedApp.Application.Common.Identity;
using MedApp.DevMcp.Tools;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <param name="Seed">The random seed (kept for the tool's contract: the demo is the same story each run).</param>
/// <param name="NowUtc">The "now" the story ends at (the real time, unless a test fixes it).</param>
/// <param name="KeepUsers">Accounts kept through the wipe (admin always is).</param>
public sealed record DemoSeedOptions(int Seed, DateTime NowUtc, IReadOnlyCollection<string> KeepUsers);

/// <summary>
/// Fills a database with the light demo's clinic (<c>MedApp.Web/src/demo</c>): Dermatology,
/// the Eye clinic and the lab, the same people, templates, patients and pathways at the same points, replayed through the app's own
/// handlers by the people who did the work (staff in their teams; the admin only sets up), so the audit trails,
/// notifications, grants and gates are the app's. Dates are relative to the day it runs, as the light demo's are. It runs
/// with a service provider of its own (<see cref="SeedServices"/>), whose clock and activity actor it moves. What's in it:
/// <c>docs/demo-data.md</c>. The local SQL Server database is wiped first (<see cref="RunAsync"/>); the browser's
/// SQLite database is made new from the model (<c>tools/MedApp.BrowserDb</c>).
/// </summary>
public static partial class DemoDataSeeder
{
    /// <summary>
    /// Loads the demo data into a database the services reach: the story, from the standard roles on. A database that
    /// may hold data is wiped first (<paramref name="wipe"/>); a new one, made from the model, needn't be.
    /// </summary>
    /// <param name="filesSkipped">Why files aren't stored, if they aren't (for the summary).</param>
    public static async Task<Dictionary<string, object?>> SeedAsync(
        SeedServices services, DemoSeedOptions options, bool wipe, string? filesSkipped, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        var timeline = new DemoTimeline(options.NowUtc);

        var keep = options.KeepUsers.Append(TestAdminTools.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The standard roles, with their permissions put back as they start (an admin may have edited them locally).
        await services.Run(sp => StandardRoleSetup.EnsureAsync(
            sp.GetRequiredService<MedAppDbContext>(), timeline.NowUtc, resetPermissions: true, ct));

        var removed = wipe ? await DemoWipe.RunAsync(services, keep, ct) : [];
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
        await DemoEyeClinic.RunAsync(seed);
        await DemoLab.CreateAsync(seed);
        await DemoClinic.CreateMdtListsAsync(seed, mdtToday, mdtAhead);
        await DemoSetup.CreateCalendarSetupAsync(seed);
        await DemoClinic.CreateBlocksAsync(seed, mdtToday, mdtAhead);
        await DemoClinic.CreateDataRequestAsync(seed);
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
