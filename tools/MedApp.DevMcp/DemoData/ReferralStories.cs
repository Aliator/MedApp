using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;

namespace MedApp.DevMcp.DemoData;

/// <summary>How a referral that didn't become a pathway ends (or hasn't yet).</summary>
public enum StoryOutcome
{
    Rejected,
    RedirectedElsewhere,
    Withdrawn,
    EnteredInError,
    AwaitingTriage,
}

/// <summary>
/// One referral that didn't become one of the story's pathways: rejected, redirected to another provider, withdrawn,
/// entered in error, or still awaiting triage now (the inbox). Its patient is chosen when it's planned, but for a
/// duplicate, chosen when it's played (someone with an open referral to that team then).
/// </summary>
public sealed class ReferralStory
{
    public required string Team { get; init; }

    /// <summary>The kind of pathway it asks for (what the reason names, and who could be referred); none for the new service.</summary>
    public PathwayKind? Kind { get; init; }
    public PatientSeed? Patient { get; set; }
    public required DateTime RegisteredUtc { get; init; }
    public required DateOnly ReceivedOn { get; init; }
    public required Priority RequestedPriority { get; init; }
    public required StoryOutcome Outcome { get; init; }
    public ReferralRejectionReason RejectionReason { get; init; }

    /// <summary>When it was decided (triaged, withdrawn or marked entered in error).</summary>
    public DateTime DecidedUtc { get; init; }

    /// <summary>The team it was first registered under, moved to <see cref="Team"/> by a triager there.</summary>
    public string? RedirectedFrom { get; init; }
    public DateTime RedirectedUtc { get; init; }

    /// <summary>On a patient with an open referral to the team: registered after the duplicate warning was confirmed.</summary>
    public bool Duplicate { get; init; }

    /// <summary>The referral, once played.</summary>
    public Referral? Referral { get; set; }
}

/// <summary>
/// Plans and plays the referrals that didn't become pathways: about one in ten rejected (each reason), a few
/// redirected to another provider and a few withdrawn, all across the 18 months at the pathways' own volumes; one
/// entered in error (registered on the wrong patient, then on the right one); a duplicate registered after its warning
/// and rejected as a duplicate; and about twenty awaiting triage now, in every team, a few past their target, one
/// moved here from another team, one a confirmed duplicate, and three for the new service nobody can triage.
/// </summary>
public static class ReferralStories
{
    /// <summary>What playing them found: how many registrations confirmed a possible duplicate.</summary>
    public sealed record Played(int DuplicatesConfirmed);

    /// <summary>
    /// Plans them, choosing their patients (someone already registered, or someone new, added to register the
    /// referral), before the patients are created. Call after the pathways' patients are assigned.
    /// </summary>
    public static List<ReferralStory> Plan(IReadOnlyList<PathwayPlan> plans, DemoPatients patients, DemoTimeline timeline, Random random)
    {
        var now = timeline.NowUtc;
        var stories = new List<ReferralStory>();
        var reasons = new[]
        {
            ReferralRejectionReason.NotOfferedHere, ReferralRejectionReason.DoesNotMeetCriteria,
            ReferralRejectionReason.InsufficientInformation, ReferralRejectionReason.Other,
        };
        var rejected = 0;

        // At the pathways' own volumes: one rejected for about every ten accepted, a few sent elsewhere or withdrawn.
        foreach (var plan in plans.Where(p => p.FromReferral && p.CreatedUtc < now.AddDays(-3)).OrderBy(p => p.RegisteredUtc))
        {
            var roll = random.NextDouble();
            var outcome = roll switch
            {
                < 0.10 => StoryOutcome.Rejected,
                < 0.117 => StoryOutcome.RedirectedElsewhere,
                < 0.134 => StoryOutcome.Withdrawn,
                _ => (StoryOutcome?)null,
            };
            if (outcome is not { } chosen)
                continue;

            var registered = DemoTimeline.InWorkingHours(plan.RegisteredUtc.AddDays(random.Next(-3, 4)), random);
            var decided = DemoTimeline.InWorkingHours(registered.AddDays(chosen == StoryOutcome.Withdrawn ? 2 + random.NextDouble() * 12 : 0.3 + random.NextDouble() * 7), random);
            if (decided >= now.AddHours(-2))
                continue;

            var reason = chosen != StoryOutcome.Rejected ? default
                : rejected < reasons.Length ? reasons[rejected]
                : PathwayPlans.Weighted(random, [(reasons[0], 30), (reasons[1], 35), (reasons[2], 25), (reasons[3], 10)]);
            if (chosen == StoryOutcome.Rejected)
                rejected++;

            var patient = random.NextDouble() < 0.45 ? patients.Existing(plan.PathwayKind, registered) : null;
            stories.Add(new ReferralStory
            {
                Team = DemoTeams.For(plan.PathwayKind),
                Kind = plan.PathwayKind,
                Patient = patient ?? patients.New(plan.PathwayKind, registered, PatientRole.ReferredOnly, justBefore: true),
                RegisteredUtc = registered,
                ReceivedOn = Received(registered),
                RequestedPriority = plan.RequestedPriority,
                Outcome = chosen,
                RejectionReason = reason,
                DecidedUtc = decided,
            });
        }

        // Entered in error: registered on the wrong patient (often one with the same surname), noticed within half an
        // hour and marked, then registered on the right one: that one became a pathway.
        var corrected = plans.Where(p => p.FromReferral && p.Fate == Fate.Normal && p.RedirectedFrom is null
                                         && p.CreatedUtc >= now.AddDays(-60) && p.CreatedUtc <= now.AddDays(-20))
            .OrderBy(_ => random.Next()).FirstOrDefault();
        if (corrected is not null)
        {
            var at = corrected.RegisteredUtc.AddMinutes(-40);
            var wrong = patients.Existing(corrected.PathwayKind, at, p => p != corrected.Patient && p.LastName == corrected.Patient.LastName)
                        ?? patients.Existing(corrected.PathwayKind, at, p => p != corrected.Patient);
            if (wrong is not null)
                stories.Add(new ReferralStory
                {
                    Team = DemoTeams.For(corrected.PathwayKind),
                    Kind = corrected.PathwayKind,
                    Patient = wrong,
                    RegisteredUtc = at,
                    ReceivedOn = corrected.ReceivedOn,
                    RequestedPriority = corrected.RequestedPriority,
                    Outcome = StoryOutcome.EnteredInError,
                    DecidedUtc = corrected.RegisteredUtc.AddMinutes(-12),
                });
        }

        // A duplicate in the past: registered a few weeks into a dermatology pathway (the warning confirmed: the
        // coordinator took it for a new lesion), then rejected as a duplicate. Its patient is found when it's played.
        var duplicateAt = DemoTimeline.InWorkingHours(now.AddDays(-random.Next(20, 40)), random);
        stories.Add(new ReferralStory
        {
            Team = DemoTeams.Dermatology,
            Kind = PathwayKind.SkinLesion,
            RegisteredUtc = duplicateAt,
            ReceivedOn = Received(duplicateAt),
            RequestedPriority = Priority.Medium,
            Outcome = StoryOutcome.Rejected,
            RejectionReason = ReferralRejectionReason.Duplicate,
            DecidedUtc = DemoTimeline.InWorkingHours(duplicateAt.AddDays(1 + random.NextDouble() * 2), random),
            Duplicate = true,
        });

        stories.AddRange(Inbox(patients, timeline, random));
        return stories.OrderBy(s => s.RegisteredUtc).ToList();

        DateOnly Received(DateTime registered)
        {
            var on = UkTime.DateOf(registered);
            return random.NextDouble() < 0.3 ? PathwayPlans.PreviousWeekday(on) : on;
        }
    }

    /// <summary>
    /// About twenty referrals awaiting triage now: in every team, at each priority, from a day to a month old, so a
    /// few are past their target (an urgent skin referral at 19 days, a high bleeding one at 30, an urgent urology one
    /// at 31, its target 28); one moved to dermatology from gynaecology three days ago; one a duplicate (registered yesterday on someone
    /// with an open dermatology pathway); and three for the new service, which nobody can triage.
    /// </summary>
    private static IEnumerable<ReferralStory> Inbox(DemoPatients patients, DemoTimeline timeline, Random random)
    {
        const string derm = DemoTeams.Dermatology, gyn = DemoTeams.Gynaecology, uro = DemoTeams.Urology,
            diabetes = DemoTeams.Diabetes, biomechanics = DemoTeams.Biomechanics, respiratory = DemoTeams.NewService;
        (string Team, PathwayKind? Kind, Priority Priority, double DaysAgo, bool Existing)[] inbox =
        [
            (derm, PathwayKind.SkinLesion, Priority.Urgent, 19, false),
            (derm, PathwayKind.SkinLesion, Priority.Urgent, 1.5, false),
            (derm, PathwayKind.SkinLesion, Priority.Medium, 9, false),
            (derm, PathwayKind.InflammatorySkin, Priority.Low, 21, false),
            (gyn, PathwayKind.PostMenopausalBleeding, Priority.Urgent, 2.5, false),
            (gyn, PathwayKind.PostMenopausalBleeding, Priority.High, 30, false),
            (gyn, PathwayKind.PostMenopausalBleeding, Priority.Medium, 16, true),
            (uro, PathwayKind.Prostate, Priority.Urgent, 31, false),
            (uro, PathwayKind.Prostate, Priority.Medium, 15, false),
            (uro, PathwayKind.Prostate, Priority.Medium, 6, true),
            (uro, PathwayKind.Prostate, Priority.Urgent, 0.8, false),
            (diabetes, PathwayKind.Diabetes, Priority.Medium, 8, false),
            (diabetes, PathwayKind.Diabetes, Priority.High, 26, true),
            (biomechanics, PathwayKind.Biomechanics, Priority.Low, 13, false),
            (biomechanics, PathwayKind.Biomechanics, Priority.Medium, 5, false),
            (respiratory, null, Priority.Medium, 20, false),
            (respiratory, null, Priority.High, 10, false),
            (respiratory, null, Priority.Urgent, 2, false),
        ];

        foreach (var (team, kind, priority, daysAgo, existing) in inbox)
        {
            var registered = Past(timeline.NowUtc.AddDays(-daysAgo), timeline, random);
            // The new service's referrals came after it was added.
            if (kind is null && registered < timeline.NewServiceAddedUtc)
                registered = timeline.NewServiceAddedUtc.AddHours(2);
            var asKind = kind ?? PathwayKind.Diabetes;
            yield return new ReferralStory
            {
                Team = team,
                Kind = kind,
                Patient = (existing ? patients.Existing(asKind, registered) : null)
                          ?? patients.New(asKind, registered, PatientRole.Waiting, justBefore: true),
                RegisteredUtc = registered,
                ReceivedOn = UkTime.DateOf(registered),
                RequestedPriority = priority,
                Outcome = StoryOutcome.AwaitingTriage,
            };
        }

        // Moved to dermatology by the gynaecologist three days ago, a week after it was registered under gynaecology.
        var movedFrom = Past(timeline.NowUtc.AddDays(-7), timeline, random);
        yield return new ReferralStory
        {
            Team = derm,
            Kind = PathwayKind.SkinLesion,
            Patient = patients.New(PathwayKind.PostMenopausalBleeding, movedFrom, PatientRole.Waiting, justBefore: true),
            RegisteredUtc = movedFrom,
            ReceivedOn = UkTime.DateOf(movedFrom),
            RequestedPriority = Priority.High,
            Outcome = StoryOutcome.AwaitingTriage,
            RedirectedFrom = gyn,
            RedirectedUtc = Past(timeline.NowUtc.AddDays(-3), timeline, random),
        };

        // A possible duplicate, registered yesterday after the warning was confirmed: its patient is found when played.
        var duplicate = Past(timeline.NowUtc.AddDays(-1), timeline, random);
        yield return new ReferralStory
        {
            Team = derm,
            Kind = PathwayKind.SkinLesion,
            RegisteredUtc = duplicate,
            ReceivedOn = UkTime.DateOf(duplicate),
            RequestedPriority = Priority.Medium,
            Outcome = StoryOutcome.AwaitingTriage,
            Duplicate = true,
        };
    }

    /// <summary>
    /// A moment in working hours at or before <paramref name="utc"/>, and before now: when the coordinator registered
    /// something, counted back from now (weekends and evenings move back to the working day before).
    /// </summary>
    public static DateTime Past(DateTime utc, DemoTimeline timeline, Random random)
    {
        var date = UkTime.DateOf(utc);
        var time = UkTime.TimeOf(utc);
        if (!WorkingHours.IsWorkingDay(date) || time < new TimeOnly(8, 30))
        {
            do date = date.AddDays(-1);
            while (!WorkingHours.IsWorkingDay(date));
            time = new TimeOnly(9, 0).AddMinutes(random.Next(0, 420));
        }
        else if (time > new TimeOnly(17, 30))
            time = new TimeOnly(9, 0).AddMinutes(random.Next(0, 420));

        var at = UkTime.ToUtc(date, time);
        return at < timeline.NowUtc.AddMinutes(-30) ? at : Past(timeline.NowUtc.AddDays(-1), timeline, random);
    }

    /// <summary>
    /// Plays them in the order they were registered, after the pathways (whose referrals the duplicate checks see):
    /// each registered by the booking coordinator; moved here by a triager of the team it came under; then
    /// rejected or redirected elsewhere by a triager of its team, or withdrawn or marked entered in error by the
    /// coordinator. The coordinator is told of each triage (the last 30 days').
    /// </summary>
    public static Played Play(DemoWorld world, IReadOnlyList<ReferralStory> stories, IReadOnlyList<PatientSeed> patients)
    {
        var random = world.Random;
        var coordinator = world.Coordinator;
        var closedAt = DemoReferrals.ClosedAt(world);
        var confirmed = 0;

        foreach (var story in stories.OrderBy(s => s.RegisteredUtc))
        {
            var at = story.RegisteredUtc;
            var team = world.Team(story.RedirectedFrom ?? story.Team);
            story.Patient ??= DuplicatePatient(world, story, patients, closedAt);
            if (story.Patient is not { } patient)
                continue;

            var kind = story.Kind is { } k ? ProcedureKinds.For(k)[random.Next(ProcedureKinds.For(k).Count)] : null;
            var (referral, events) = DemoReferrals.Register(
                patient.Id, team, story.ReceivedOn, story.RequestedPriority, ReferralTexts.Reason(kind, random), coordinator, at, random);
            // The register page's warning (decision 12): the coordinator confirmed it was a different referral.
            if (DemoReferrals.OpenAt(world, patient.Id, team.Id, at, closedAt).Count > 0)
                confirmed++;
            world.Referrals.Add(referral);
            world.ReferralEvents.AddRange(events);
            world.Touches.Add(new Touch(coordinator.Id, at, patient.Id, null, referral.Id));
            story.Referral = referral;

            if (story.RedirectedFrom is { } from)
            {
                var mover = DemoReferrals.Triager(world, from, story.RedirectedUtc)
                            ?? throw new InvalidOperationException($"Seeding went wrong: nobody could triage {from} referrals.");
                var target = world.Team(story.Team);
                var move = DemoReferrals.Triage(referral, mover, story.RedirectedUtc);
                move.RedirectToTeam(target, ReferralTexts.RedirectDetail(from, story.Team));
                referral.Team = target;
                Record(move, mover, story.RedirectedUtc, from);
            }

            switch (story.Outcome)
            {
                case StoryOutcome.Rejected or StoryOutcome.RedirectedElsewhere:
                {
                    var triager = DemoReferrals.Triager(world, story.Team, story.DecidedUtc)
                                  ?? throw new InvalidOperationException($"Seeding went wrong: nobody could triage {story.Team} referrals.");
                    var change = DemoReferrals.Triage(referral, triager, story.DecidedUtc);
                    if (story.Outcome == StoryOutcome.Rejected)
                        change.Reject(story.RejectionReason, ReferralTexts.RejectionDetail(story.RejectionReason, random));
                    else
                    {
                        var (elsewhere, detail) = ReferralTexts.Elsewhere(story.Team, random);
                        change.RedirectElsewhere(elsewhere, detail);
                    }

                    Record(change, triager, story.DecidedUtc);
                    break;
                }
                case StoryOutcome.Withdrawn or StoryOutcome.EnteredInError:
                {
                    // Holders of RegisterReferrals close a referral awaiting triage (decision 14): the coordinator.
                    if (!ReferralWorkflow.IsOpen(referral.Status))
                        throw new InvalidOperationException($"Seeding went wrong: {ReferralWorkflow.ClosedMessage(referral.Status)}");
                    var change = new ReferralChange(referral, coordinator.Actor, story.DecidedUtc);
                    if (story.Outcome == StoryOutcome.Withdrawn)
                        change.Withdraw(ReferralTexts.WithdrawalDetail(random));
                    else
                        change.MarkEnteredInError(ReferralTexts.EnteredInErrorDetail);
                    world.ReferralEvents.AddRange(change.Events);
                    world.Touches.Add(new Touch(coordinator.Id, story.DecidedUtc, patient.Id, null, referral.Id));
                    break;
                }
            }
        }

        return new Played(confirmed);

        void Record(ReferralChange change, StaffMember triager, DateTime when, string? previousTeam = null)
        {
            var referral = change.Referral;
            world.ReferralEvents.AddRange(change.Events);
            world.Touches.Add(new Touch(triager.Id, when, referral.PatientId, null, referral.Id));
            if (DemoReferrals.Told(world, referral, triager, when, previousTeam) is { } told)
                world.Notifications.Add(told);
        }
    }

    /// <summary>
    /// The patient for a duplicate: someone with an accepted dermatology referral whose pathway is open at the moment
    /// it's registered (and still open now, for the one awaiting triage), on the system then, and not restricted.
    /// </summary>
    private static PatientSeed? DuplicatePatient(
        DemoWorld world, ReferralStory story, IReadOnlyList<PatientSeed> patients, IReadOnlyDictionary<Guid, DateTime> closedAt)
    {
        var team = world.Team(story.Team);
        var byId = patients.ToDictionary(p => p.Id);
        var candidates = world.Referrals
            .Where(r => r.Status == ReferralStatus.Accepted && r.TeamId == team.Id && r.DecidedAtUtc < story.RegisteredUtc.AddDays(-7)
                        && r.PathwayId is { } id && !closedAt.ContainsKey(id)
                        && world.Pathways.Any(p => p.Id == id && PathwayWorkflow.IsOpen(p.Status))
                        && byId.TryGetValue(r.PatientId, out var p) && p.IsOnSystemAt(story.RegisteredUtc) && !p.Restricted)
            .Select(r => byId[r.PatientId])
            .Distinct()
            .OrderBy(p => p.FullName)
            .ToList();
        return candidates.Count == 0 ? null : candidates[world.Random.Next(candidates.Count)];
    }
}
