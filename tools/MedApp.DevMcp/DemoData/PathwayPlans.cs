using MedApp.Domain.Pathways.PatientPathways;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;

namespace MedApp.DevMcp.DemoData;

/// <summary>How a referral's story ends, beyond running its course.</summary>
public enum Fate
{
    Normal,

    /// <summary>Blocked when a stage was reached, and still blocked.</summary>
    Blocked,

    /// <summary>Blocked, then unblocked days later, and carried on.</summary>
    Unblocked,

    /// <summary>Cancelled when a stage was reached.</summary>
    Cancelled,

    /// <summary>Cancelled after its visit was booked: the booked appointment is cancelled by hand, with nothing to replace it.</summary>
    CancelledAfterBooking,

    /// <summary>Cancelled, then deleted by an admin weeks later (its audit remains).</summary>
    Deleted,
}

/// <summary>One referral to replay: when it came in, what for, and the twists its story takes.</summary>
public sealed class PathwayPlan
{
    public required PathwayKindSpec Spec { get; init; }
    public PathwayKind PathwayKind => Spec.PathwayKind;
    public required DateTime CreatedUtc { get; init; }
    public required ProcedureKind Kind { get; init; }
    /// <summary>Where on the body, for a kind with a site: chosen once, so every note names the same place.</summary>
    public string? Side { get; init; }

    /// <summary>Which of the kind's outcomes this one has: chosen once, so its notes agree from stage to stage.</summary>
    public int Outcome { get; init; }
    public required Priority Priority { get; init; }
    public bool Urgent => Priority == Priority.Urgent;
    public PatientSeed Patient { get; set; } = null!;

    public Fate Fate { get; set; }
    public int FateStage { get; set; } = -1;

    /// <summary>A pathway from the old template still finishing long after the template was archived.</summary>
    public bool Straggler { get; set; }

    /// <summary>A stage whose visit was asked for and never booked: it sits in Requires Appointment.</summary>
    public int NeverBookStage { get; set; } = -1;

    /// <summary>A Required stage an admin finished without its visit, with a reason.</summary>
    public int BypassStage { get; set; } = -1;

    /// <summary>A finished stage an admin unlocked, corrected and finished again.</summary>
    public int UnlockStage { get; set; } = -1;

    /// <summary>The stage before which the lead was changed.</summary>
    public int ReassignStage { get; set; } = -1;

    public bool SecondAssignee { get; set; }
    public bool EditDueDate { get; set; }
    public bool RaisePriority { get; set; }
    public bool EditNotes { get; set; }

    /// <summary>The pathway replayed from this plan (set once its replay is kept).</summary>
    public Guid PathwayId { get; set; }

    public NoteContext NoteContext(int occurrence = 1, int occurrences = 1) => new(Kind, Side, Urgent, occurrence, occurrences, Outcome);
}

/// <summary>Plans every referral: when each pathway's came in (<see cref="VolumeCurves"/>), and their twists.</summary>
public static class PathwayPlans
{
    public static List<PathwayPlan> Create(DemoTimeline timeline, Random random)
    {
        var plans = new List<PathwayPlan>();
        foreach (var spec in DemoPathwayKinds.PathwayKinds)
        {
            var kinds = ProcedureKinds.For(spec.PathwayKind);
            foreach (var created in VolumeCurves.CreationTimes(spec.PathwayKind, timeline.NowUtc, random))
            {
                var kind = kinds[random.Next(kinds.Count)];
                plans.Add(new PathwayPlan
                {
                    Spec = spec,
                    CreatedUtc = created,
                    Kind = kind,
                    Side = kind.Sites.Length > 0 ? kind.Sites[random.Next(kind.Sites.Length)] : null,
                    Outcome = random.Next(kind.Outcomes.Length),
                    Priority = Weighted(random, spec.Priorities),
                });
            }
        }

        plans = plans.OrderBy(p => p.CreatedUtc).ToList();
        AddTwists(plans, timeline, random);
        return plans;
    }

    private static void AddTwists(List<PathwayPlan> plans, DemoTimeline timeline, Random random)
    {
        var now = timeline.NowUtc;
        bool Between(PathwayPlan p, double fromDaysAgo, double toDaysAgo) =>
            p.CreatedUtc >= now.AddDays(-fromDaysAgo) && p.CreatedUtc <= now.AddDays(-toDaysAgo);
        bool Free(PathwayPlan p) => p.Fate == Fate.Normal && !p.Straggler;

        // The old skin lesion pathway's last few, still finishing.
        foreach (var plan in plans.Where(p => p.PathwayKind == PathwayKind.SkinLesion2024).TakeLast(3))
            plan.Straggler = true;

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 70, 7) && p.PathwayKind is PathwayKind.Prostate or PathwayKind.PostMenopausalBleeding or PathwayKind.SkinLesion or PathwayKind.InflammatorySkin, 6))
            (plan.Fate, plan.FateStage) = (Fate.Blocked, random.Next(1, Math.Min(4, plan.Spec.Stages.Length)));

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 150, 80) && p.PathwayKind is PathwayKind.Prostate or PathwayKind.PostMenopausalBleeding, 2))
            (plan.Fate, plan.FateStage) = (Fate.Unblocked, random.Next(1, 4));

        foreach (var plan in Choose(plans, p => Free(p) && p.CreatedUtc < now.AddDays(-10), 12))
            (plan.Fate, plan.FateStage) = (Fate.Cancelled, random.Next(0, plan.Spec.Stages.Length - 1));

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 120, 14) && FirstVisitStage(p, from: 1) >= 0, 5))
            (plan.Fate, plan.FateStage) = (Fate.CancelledAfterBooking, FirstVisitStage(plan, from: 1));

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 200, 70), 2))
            (plan.Fate, plan.FateStage) = (Fate.Deleted, random.Next(0, 3));

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 90, 40) && p.PathwayKind is PathwayKind.Prostate or PathwayKind.PostMenopausalBleeding, 4))
            plan.NeverBookStage = FirstVisitStage(plan, from: 1);

        // The bloods and the sample were taken elsewhere, so an admin finished that stage without its visit.
        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 180, 60) && p.PathwayKind is PathwayKind.Prostate or PathwayKind.Diabetes, 2))
            plan.BypassStage = Array.FindIndex(plan.Spec.Stages, s => s.Definition == DemoPathwayKinds.BloodAndUrineTests);

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 180, 21) && p.Spec.Stages.Length >= 4, 6))
            plan.UnlockStage = random.Next(1, Math.Min(4, plan.Spec.Stages.Length - 1));

        foreach (var plan in plans)
        {
            // A nurse specialist (or the pharmacist) works alongside the lead on some skin lesion and diabetes pathways;
            // now and then a skin lesion changes hands between the two skin clinicians.
            var skinLesion = plan.PathwayKind is PathwayKind.SkinLesion or PathwayKind.SkinLesion2024;
            plan.SecondAssignee = (skinLesion || plan.PathwayKind == PathwayKind.Diabetes) && random.NextDouble() < 0.25;
            if (skinLesion && Free(plan) && random.NextDouble() < 0.05)
                plan.ReassignStage = random.Next(2, Math.Min(5, plan.Spec.Stages.Length));
            plan.EditDueDate = random.NextDouble() < (plan.PathwayKind == PathwayKind.Prostate ? 0.3 : 0.06);
            plan.RaisePriority = plan.PathwayKind == PathwayKind.PostMenopausalBleeding && !plan.Urgent && random.NextDouble() < 0.12;
            plan.EditNotes = random.NextDouble() < 0.07;
        }

        IEnumerable<PathwayPlan> Choose(IEnumerable<PathwayPlan> source, Func<PathwayPlan, bool> eligible, int count)
        {
            var pool = source.Where(eligible).ToList();
            var chosen = new List<PathwayPlan>();
            for (var i = 0; i < count && pool.Count > 0; i++)
            {
                var index = random.Next(pool.Count);
                chosen.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return chosen;
        }
    }

    /// <summary>The first stage from <paramref name="from"/> with a visit that's always booked, or -1.</summary>
    private static int FirstVisitStage(PathwayPlan plan, int from)
    {
        for (var i = from; i < plan.Spec.Stages.Length; i++)
        {
            var stage = plan.Spec.Stages[i];
            if (stage.VisitMinutes > 0 && stage.VisitChance >= 1 && stage.Role != StageRole.Coordinator)
                return i;
        }

        return -1;
    }

    public static T Weighted<T>(Random random, (T Item, int Weight)[] weights)
    {
        var roll = random.Next(weights.Sum(w => w.Weight));
        foreach (var (item, weight) in weights)
        {
            if (roll < weight)
                return item;
            roll -= weight;
        }

        return weights[^1].Item;
    }
}
