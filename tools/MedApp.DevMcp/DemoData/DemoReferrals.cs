using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Referrals, through the app's own rules as its handlers apply them: <see cref="ReferralChange"/> for registering,
/// triaging (accept, reject, redirect), withdrawing and marking entered in error, and the history each writes;
/// <see cref="WaitingClock"/> for the clock; <see cref="ReferralRules"/> and <see cref="ReferralWorkflow"/> for what's
/// allowed; <see cref="ReferralNotifications"/> for what the person who registered one is told when it's triaged.
/// Accepting one into a pathway is part of its pathway's replay (<see cref="PathwayReplay"/>); the referrals that never
/// became pathways, and those still awaiting triage now, are played here (<see cref="ReferralStories"/>).
/// </summary>
public static class DemoReferrals
{
    /// <summary>
    /// Whether someone could triage a team's referrals at a moment, as the app checks (decision 18): their roles
    /// then grant <c>TriageReferrals</c>, they belong to the team (their job's teams, given minutes after their
    /// account was made), and they work here.
    /// </summary>
    public static bool CanTriage(StaffMember member, string team, DateTime utc) =>
        member.Kind != StaffKind.Leaver
        && DemoTeams.For(member.Kind).Contains(team)
        && member.WorksAt(utc)
        && utc >= member.StartedUtc.AddMinutes(10)
        && member.PermissionsAt(utc).Contains(Permission.TriageReferrals);

    /// <summary>
    /// Who triages a team's referral at a moment: usually <paramref name="prefer"/> (the pathway's lead) if they can,
    /// otherwise one of the team's clinicians, now and then the clinical lead or the pharmacist (the lead takes most
    /// diabetes referrals, as its nurse specialists' role doesn't triage). Null if nobody can (the new service).
    /// </summary>
    public static StaffMember? Triager(DemoWorld world, string team, DateTime utc, StaffMember? prefer = null)
    {
        if (prefer is not null && CanTriage(prefer, team, utc) && world.Random.NextDouble() < 0.75)
            return prefer;

        var eligible = world.Staff.Where(s => CanTriage(s, team, utc)).ToList();
        if (eligible.Count == 0)
            return null;

        return PathwayPlans.Weighted(world.Random, eligible.Select(s => (s, s.Kind switch
        {
            StaffKind.ClinicalLead => team == DemoTeams.Diabetes ? 8 : 1,
            StaffKind.Pharmacist => 1,
            _ => 6,
        })).ToArray());
    }

    /// <summary>
    /// A referral registered by <paramref name="by"/> at <paramref name="at"/>, as RegisterReferralHandler makes it: the
    /// details typed from the letter, Awaiting triage, and its <c>Registered</c> entry, with its clock started on the day it
    /// was received with the team's target for the priority asked for. Its team is attached (in memory) for
    /// the rules and messages that name it.
    /// </summary>
    public static (Referral Referral, IReadOnlyList<ReferralEvent> Events) Register(
        Guid patientId, Team team, DateOnly receivedOn, Priority requested, string reason, StaffMember by, DateTime at, Random random)
    {
        if (ReferralRules.WhyReceivedOnNotAllowed(receivedOn, DueDates.ToUkDate(at)) is { } why)
            throw new InvalidOperationException($"Seeding went wrong: {why}");
        if (!by.PermissionsAt(at).Contains(Permission.RegisterReferrals))
            throw new InvalidOperationException($"Seeding went wrong: {by.Username} can't register referrals.");

        var referral = new Referral
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            TeamId = team.Id,
            Team = team,
            ReceivedOn = receivedOn,
            ReferrerPractice = ReferralTexts.Practice(random),
            ReferrerClinician = ReferralTexts.Clinician(random),
            RequestedPriority = requested,
            Reason = reason,
            Status = ReferralStatus.AwaitingTriage,
            RegisteredAtUtc = at,
            RegisteredByUserId = by.Id,
            RegisteredByUsername = by.Username,
            LastUpdated = at,
        };
        var change = new ReferralChange(referral, by.Actor, at);
        change.Register(team);
        return (referral, change.Events);
    }

    /// <summary>
    /// A triage decision at <paramref name="at"/> by <paramref name="triager"/> on an open referral, as the triage handlers
    /// make it (they check the permission, the membership and that it's still open first; so does this).
    /// </summary>
    public static ReferralChange Triage(Referral referral, StaffMember triager, DateTime at)
    {
        var team = referral.Team.Name;
        if (!CanTriage(triager, team, at))
            throw new InvalidOperationException($"Seeding went wrong: {triager.Username} can't triage {team} referrals then.");
        if (!ReferralWorkflow.IsOpen(referral.Status))
            throw new InvalidOperationException($"Seeding went wrong: {ReferralWorkflow.AlreadyTriagedMessage(referral)}");
        return new ReferralChange(referral, triager.Actor, at);
    }

    /// <summary>
    /// What the person who registered a referral is told of a triage at a moment (the last 30 days' only, as for every
    /// notification in the demo), if they'd get it.
    /// </summary>
    public static Notification? Told(DemoWorld world, Referral referral, StaffMember triager, DateTime at, string? previousTeamName = null)
    {
        if (at < world.Timeline.NotificationsFromUtc)
            return null;
        var notification = ReferralNotifications.Triaged(referral, triager.Actor, at, previousTeamName);
        return notification is not null && world.Delivers(notification, at) ? notification : null;
    }

    /// <summary>
    /// The patient's open referrals to a team at a moment, as the registration's duplicate check finds them
    /// (decision 12): awaiting triage then, or accepted with the pathway still open.
    /// </summary>
    public static IReadOnlyList<Referral> OpenAt(DemoWorld world, Guid patientId, Guid teamId, DateTime at, IReadOnlyDictionary<Guid, DateTime> closedAt) =>
        world.Referrals.Where(r => r.PatientId == patientId && r.TeamId == teamId && r.RegisteredAtUtc <= at
                                   && (r.DecidedAtUtc is null || r.DecidedAtUtc > at
                                       || (r.Status == ReferralStatus.Accepted && r.PathwayId is { } id && (!closedAt.TryGetValue(id, out var closed) || closed > at))))
            .ToList();

    /// <summary>When each pathway was first discharged, cancelled or archived (its audit's status changes).</summary>
    public static Dictionary<Guid, DateTime> ClosedAt(DemoWorld world) =>
        world.Audit.Where(a => a.Action == PatientPathwayAuditAction.StatusChanged
                               && (a.Details.Contains("\"to\":\"Completed\"") || a.Details.Contains("\"to\":\"Cancelled\"") || a.Details.Contains("\"to\":\"Archived\"")))
            .GroupBy(a => a.PatientPathwayId)
            .ToDictionary(g => g.Key, g => g.Min(a => a.AtUtc));

    /// <summary>
    /// Saves the referrals (each with its clock's pauses) and their history, after the pathways they point at. The
    /// navigations attached for the rules (team, pathway) are let go first, so only the referrals are added.
    /// </summary>
    public static async Task SaveAsync(SeedServices services, DemoWorld world, CancellationToken ct)
    {
        foreach (var referral in world.Referrals)
        {
            referral.Team = null!;
            referral.Pathway = null;
        }

        await Batch(world.Referrals, (db, items) => db.Referrals.AddRange(items), 500);
        await Batch(world.ReferralEvents, (db, items) => db.ReferralEvents.AddRange(items), 4000);

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
    public static object Summarise(DemoWorld world, ReferralStories.Played played)
    {
        var today = world.Timeline.Today;
        var referrals = world.Referrals;
        var teamNames = world.Teams.ToDictionary(s => s.Key, s => s.Value.Name);
        var awaiting = referrals.Where(r => r.Status == ReferralStatus.AwaitingTriage).ToList();
        var clocks = referrals.Where(r => r.Status == ReferralStatus.Accepted).ToList();
        return new
        {
            total = referrals.Count,
            byStatus = referrals.GroupBy(r => r.Status).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            awaitingTriage = new
            {
                byTeam = awaiting.GroupBy(r => teamNames[r.TeamId]).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
                breached = awaiting.Count(r => WaitingClock.IsBreached(r, today)),
            },
            rejectedByReason = referrals.Where(r => r.Status == ReferralStatus.Rejected)
                .GroupBy(r => r.RejectionReason!.Value).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            redirectedToAnotherTeam = world.ReferralEvents.Count(e => e.Action == ReferralEventAction.TeamChanged),
            duplicateWarningsConfirmed = played.DuplicatesConfirmed,
            pathwaysWithoutAReferral = world.Pathways.Count(p => p.TemplateId is not null && !referrals.Any(r => r.PathwayId == p.Id)),
            clocks = new
            {
                running = clocks.Count(WaitingClock.IsRunning),
                paused = clocks.Count(WaitingClock.IsPaused),
                pauseReviewOverdue = clocks.Count(r => WaitingClock.IsReviewOverdue(r, today)),
                stopped = clocks.Count(WaitingClock.IsStopped),
                breachedOpen = clocks.Count(r => !WaitingClock.IsStopped(r) && WaitingClock.IsBreached(r, today)),
                breachedStopped = clocks.Count(r => WaitingClock.IsStopped(r) && WaitingClock.IsBreached(r, today)),
                pauses = referrals.Sum(r => r.Pauses.Count),
            },
            history = world.ReferralEvents.Count,
            triagedNotifications = world.Notifications.Count(n => n.Kind == NotificationKind.ReferralTriaged),
        };
    }
}
