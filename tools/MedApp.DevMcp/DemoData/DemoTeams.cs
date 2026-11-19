using MediatR;
using MedApp.Application.Authentication.Users.Commands.SetUserTeams;
using MedApp.Application.Teams.Commands.CreateTeam;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The centre's teams (its services), and which staff belong to each. Made through the app's commands, as an
/// admin made them: the teams when the centre started, each person's memberships a few minutes after their
/// account (recorded in the activity log as <c>TeamMembershipChanged</c>). General, which every database starts with,
/// stays, with nothing in it. Each has its own waiting-time targets (<see cref="Targets"/>). A sixth, Respiratory, is a
/// new service added three weeks ago whose clinician hasn't started: nobody belongs to it, so nobody can triage its
/// referrals, and the Teams page warns.
/// </summary>
public static class DemoTeams
{
    public const string Dermatology = "Dermatology";
    public const string Gynaecology = "Gynaecology";
    public const string Urology = "Urology";
    public const string Diabetes = "Diabetes";
    public const string Biomechanics = "Biomechanics";

    /// <summary>The new service: no members, no templates yet, and referrals already waiting for triage.</summary>
    public const string NewService = "Respiratory";

    /// <summary>The centre's services from the start (the new service isn't one of them).</summary>
    public static readonly string[] Names = [Dermatology, Gynaecology, Urology, Diabetes, Biomechanics];

    /// <summary>
    /// Each team's waiting-time targets in days, by priority: the centre's own administrative targets (decision 29),
    /// not national rules. Urgent referrals 14 days (28 for urology, whose visiting consultant's clinic is weekly);
    /// routine skin 126; biomechanics routine 84. The urologist's clinic is the slowest to reach, so about half its
    /// waits breach, and urgent skin and bleeding referrals breach now and then. The new service keeps the app's
    /// defaults.
    /// </summary>
    public static (int Urgent, int High, int Medium, int Low) Targets(string name) => name switch
    {
        Dermatology => (14, 28, 126, 126),
        Gynaecology => (14, 28, 63, 126),
        Urology => (28, 56, 63, 126),
        Diabetes => (14, 42, 84, 126),
        Biomechanics => (14, 28, 84, 84),
        _ => (14, 28, 126, 126),
    };

    /// <summary>
    /// Which teams each kind of staff belongs to: consultants, the GP with a special interest, the nurse
    /// specialists and the podiatrist in theirs, the pharmacist in several, and the clinical lead (the local
    /// <c>admin</c>) in every one, so it can show triage. Everyone else (the manager, the booking coordinator, the lab,
    /// the healthcare assistant, the new starter) in none.
    /// </summary>
    public static IReadOnlyList<string> For(StaffKind kind) => kind switch
    {
        StaffKind.ClinicalLead => Names,
        StaffKind.Dermatologist or StaffKind.SkinGp or StaffKind.SkinCancerNurse => [Dermatology],
        StaffKind.Gynaecologist => [Gynaecology],
        StaffKind.Urologist => [Urology],
        StaffKind.DiabetesNurse => [Diabetes],
        StaffKind.Podiatrist => [Biomechanics],
        StaffKind.Pharmacist => [Dermatology, Urology, Diabetes],
        _ => [],
    };

    /// <summary>The team each pathway (template) belongs to, the archived skin lesion pathway too.</summary>
    public static string For(PathwayKind kind) => kind switch
    {
        PathwayKind.SkinLesion or PathwayKind.SkinLesion2024 or PathwayKind.InflammatorySkin => Dermatology,
        PathwayKind.PostMenopausalBleeding => Gynaecology,
        PathwayKind.Prostate => Urology,
        PathwayKind.Diabetes => Diabetes,
        PathwayKind.Biomechanics => Biomechanics,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A pathway with no team."),
    };

    /// <summary>The teams' IDs by name, and when the last was added.</summary>
    public sealed record Created(IReadOnlyDictionary<string, Guid> Ids, DateTime AddedUtc);

    /// <summary>
    /// Adds the teams with their targets, as the clinical lead did before the centre opened, and the new service
    /// three weeks ago.
    /// </summary>
    public static async Task<Created> CreateAsync(
        SeedServices services, StaffMember lead, DemoTimeline timeline, CancellationToken ct)
    {
        var ids = new Dictionary<string, Guid>();
        var at = timeline.AppStartUtc.AddDays(-30).AddHours(1);
        foreach (var name in Names)
        {
            ids[name] = await AddAsync(name, at);
            at = at.AddMinutes(2);
        }

        ids[NewService] = await AddAsync(NewService, timeline.NewServiceAddedUtc);
        return new Created(ids, at);

        async Task<Guid> AddAsync(string name, DateTime when)
        {
            var (urgent, high, medium, low) = Targets(name);
            services.At(when, lead.ActivityActor);
            var team = await services.Run(sp => Guards.Validated(() =>
                sp.GetRequiredService<IMediator>().Send(new CreateTeamCommand(name, urgent, high, medium, low), ct)));
            return team.Id;
        }
    }

    /// <summary>The teams as stored (their targets), by ID: what referrals' clocks read their targets from.</summary>
    public static Task<Dictionary<Guid, Team>> LoadAsync(SeedServices services, CancellationToken ct) =>
        services.Run(sp => sp.GetRequiredService<MedAppDbContext>().Teams.AsNoTracking().ToDictionaryAsync(s => s.Id, ct));

    /// <summary>
    /// Gives each member of staff their teams, a few minutes after their account was made (or after the
    /// teams were, for anyone whose account is older). Returns how many memberships there are.
    /// </summary>
    public static async Task<int> AddMembershipsAsync(
        SeedServices services, IReadOnlyList<StaffMember> staff, Created teams, CancellationToken ct)
    {
        var lead = staff[0];
        var count = 0;
        // An admin is in no team (MC3, decision 209): the clinical lead and the service manager (an admin later) get
        // none. MC12 moves their clinical work to staff accounts.
        foreach (var member in staff.Where(m => m.Kind is not (StaffKind.Leaver or StaffKind.ClinicalLead or StaffKind.ServiceManager)))
        {
            var names = For(member.Kind);
            if (names.Count == 0)
                continue;

            var from = member.StartedUtc > teams.AddedUtc ? member.StartedUtc : teams.AddedUtc;
            services.At(from.AddMinutes(2), lead.ActivityActor);
            await services.Run(sp => Guards.Validated(() =>
                sp.GetRequiredService<IMediator>().Send(
                    new SetUserTeamsCommand(member.Username, names.Select(n => new TeamChoice(teams.Ids[n])).ToList()), ct)));
            count += names.Count;
        }

        return count;
    }
}
