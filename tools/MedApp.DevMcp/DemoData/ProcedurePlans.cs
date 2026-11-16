using MedApp.Domain.Procedures.PatientProcedures;
using Priority = MedApp.Domain.Procedures.PatientProcedures.PatientProcedurePriority;

namespace MedApp.DevMcp.DemoData;

/// <summary>How a procedure's story ends, beyond running its course.</summary>
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

/// <summary>One procedure to replay: when it was referred, what for, and the twists its story takes.</summary>
public sealed class ProcedurePlan
{
    public required PathwaySpec Spec { get; init; }
    public Pathway Pathway => Spec.Pathway;
    public required DateTime CreatedUtc { get; init; }
    public required ProcedureKind Kind { get; init; }
    public string? Side { get; init; }
    public required Priority Priority { get; init; }
    public bool Urgent => Priority == Priority.Urgent;
    public PatientSeed Patient { get; set; } = null!;

    public Fate Fate { get; set; }
    public int FateStage { get; set; } = -1;

    /// <summary>An old-pathway procedure still finishing long after the pathway was archived.</summary>
    public bool Straggler { get; set; }

    /// <summary>A stage whose visit was asked for and never booked: it sits in Needs booking.</summary>
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

    public NoteContext NoteContext(int occurrence = 1, int occurrences = 1) => new(Kind, Side, Urgent, occurrence, occurrences);
}

/// <summary>Plans every procedure: when each pathway's were referred (<see cref="VolumeCurves"/>), and their twists.</summary>
public static class ProcedurePlans
{
    public static List<ProcedurePlan> Create(DemoTimeline timeline, Random random)
    {
        var plans = new List<ProcedurePlan>();
        foreach (var spec in DemoPathways.Pathways)
        {
            var kinds = ProcedureKinds.For(spec.Pathway);
            foreach (var created in VolumeCurves.CreationTimes(spec.Pathway, timeline.NowUtc, random))
            {
                var kind = kinds[random.Next(kinds.Count)];
                plans.Add(new ProcedurePlan
                {
                    Spec = spec,
                    CreatedUtc = created,
                    Kind = kind,
                    Side = kind.Side ? (random.NextDouble() < 0.5 ? "left" : "right") : null,
                    Priority = Weighted(random, spec.Priorities),
                });
            }
        }

        plans = plans.OrderBy(p => p.CreatedUtc).ToList();
        AddTwists(plans, timeline, random);
        return plans;
    }

    private static void AddTwists(List<ProcedurePlan> plans, DemoTimeline timeline, Random random)
    {
        var now = timeline.NowUtc;
        bool Between(ProcedurePlan p, double fromDaysAgo, double toDaysAgo) =>
            p.CreatedUtc >= now.AddDays(-fromDaysAgo) && p.CreatedUtc <= now.AddDays(-toDaysAgo);
        bool Free(ProcedurePlan p) => p.Fate == Fate.Normal && !p.Straggler;

        // The old day-case pathway's last few, still finishing.
        foreach (var plan in plans.Where(p => p.Pathway == Pathway.DayCase2024).TakeLast(3))
            plan.Straggler = true;

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 70, 7) && p.Pathway is Pathway.Surgical or Pathway.Diagnostic or Pathway.DayCase or Pathway.Minor, 6))
            (plan.Fate, plan.FateStage) = (Fate.Blocked, random.Next(1, Math.Min(4, plan.Spec.Stages.Length)));

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 150, 80) && p.Pathway is Pathway.Surgical or Pathway.Diagnostic, 2))
            (plan.Fate, plan.FateStage) = (Fate.Unblocked, random.Next(1, 4));

        foreach (var plan in Choose(plans, p => Free(p) && p.CreatedUtc < now.AddDays(-10), 12))
            (plan.Fate, plan.FateStage) = (Fate.Cancelled, random.Next(0, plan.Spec.Stages.Length - 1));

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 120, 14) && FirstVisitStage(p, from: 1) >= 0, 5))
            (plan.Fate, plan.FateStage) = (Fate.CancelledAfterBooking, FirstVisitStage(plan, from: 1));

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 200, 70), 2))
            (plan.Fate, plan.FateStage) = (Fate.Deleted, random.Next(0, 3));

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 90, 40) && p.Pathway is Pathway.Surgical or Pathway.Diagnostic, 4))
            plan.NeverBookStage = FirstVisitStage(plan, from: 1);

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 180, 60) && p.Pathway is Pathway.Surgical or Pathway.DayCase2024, 2))
            plan.BypassStage = Array.FindIndex(plan.Spec.Stages, s => s.Definition == "Pre-assessment");

        foreach (var plan in Choose(plans, p => Free(p) && Between(p, 180, 21) && p.Spec.Stages.Length >= 4, 6))
            plan.UnlockStage = random.Next(1, Math.Min(4, plan.Spec.Stages.Length - 1));

        foreach (var plan in plans)
        {
            var surgical = plan.Pathway is Pathway.Surgical or Pathway.DayCase or Pathway.DayCase2024;
            plan.SecondAssignee = surgical && random.NextDouble() < 0.25;
            if (surgical && Free(plan) && random.NextDouble() < 0.05)
                plan.ReassignStage = random.Next(2, Math.Min(5, plan.Spec.Stages.Length));
            plan.EditDueDate = random.NextDouble() < (plan.Pathway == Pathway.Surgical ? 0.15 : 0.04);
            plan.RaisePriority = plan.Pathway == Pathway.Diagnostic && !plan.Urgent && random.NextDouble() < 0.08;
            plan.EditNotes = random.NextDouble() < 0.07;
        }

        IEnumerable<ProcedurePlan> Choose(IEnumerable<ProcedurePlan> source, Func<ProcedurePlan, bool> eligible, int count)
        {
            var pool = source.Where(eligible).ToList();
            var chosen = new List<ProcedurePlan>();
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
    private static int FirstVisitStage(ProcedurePlan plan, int from)
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
