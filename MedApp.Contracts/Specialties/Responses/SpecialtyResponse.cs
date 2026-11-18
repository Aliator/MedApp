namespace MedApp.Contracts.Specialties.Responses;

public sealed class SpecialtyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Archived specialties can't be chosen for templates or given to staff; what has one keeps it.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Send it back as <c>expectedVersion</c> with a change.</summary>
    public int Version { get; set; }

    /// <summary>How many pathway templates belong to it (archived ones too).</summary>
    public int TemplateCount { get; set; }

    /// <summary>How many pathways have it.</summary>
    public int PathwayCount { get; set; }

    /// <summary>How many members of staff belong to it.</summary>
    public int MemberCount { get; set; }

    /// <summary>How many referrals ask for it, in any status.</summary>
    public int ReferralCount { get; set; }

    /// <summary>
    /// How many of its members can triage its referrals (they hold the TriageReferrals permission). With none, nobody
    /// can triage its referrals, and the Specialties page warns (hazard H-40).
    /// </summary>
    public int TriagerCount { get; set; }

    /// <summary>Whether it can be deleted: only while no template, pathway or member of staff has it.</summary>
    public bool CanBeDeleted { get; set; }

    /// <summary>The waiting-time targets, in days, by priority: the centre's own, copied by each new referral's clock.</summary>
    public int TargetDaysUrgent { get; set; }

    public int TargetDaysHigh { get; set; }

    public int TargetDaysMedium { get; set; }

    public int TargetDaysLow { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
}
