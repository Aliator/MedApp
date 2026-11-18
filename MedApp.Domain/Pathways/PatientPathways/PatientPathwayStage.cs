namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// One stage of a patient's pathway. Its name, description, instructions and SNOMED CT code are copied from the stage
/// definition when the pathway is created, so later edits to the definition don't rewrite a record of work.
/// Stages are worked in order: started, notes written, then finished, which locks them until an admin unlocks.
/// </summary>
public sealed class PatientPathwayStage
{
    public Guid Id { get; init; }

    public Guid PatientPathwayId { get; set; }
    public PatientPathway PatientPathway { get; set; } = null!;

    public Guid StageDefinitionId { get; set; }
    public PatientPathwayStageDefinition StageDefinition { get; set; } = null!;

    public int StageOrder { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    /// <summary>The definition's SNOMED CT code and term, copied with its name when the pathway was created.</summary>
    public string? SnomedCode { get; set; }
    public string? SnomedTerm { get; set; }
    /// <summary>Copied from the stage definition: whether the stage involves a visit (an appointment).</summary>
    public VisitRequirement VisitRequirement { get; set; }

    /// <summary>
    /// Copied from the stage definition: the kind of letter the stage needs (decision 105). It can't be finished until a
    /// letter of that kind on the pathway is approved and sent to every recipient; null for none.
    /// </summary>
    public Letters.LetterKind? NeedsLetterKind { get; set; }

    /// <summary>
    /// Copied from the stage definition: whether the stage waits for results (decision 95). It can't be finished while
    /// any result of the pathway awaits sign-off.
    /// </summary>
    public bool WaitsForResults { get; set; }

    /// <summary>
    /// Whether the stage needs a Valid consent record before it can be finished (decision 146); copied onto each
    /// pathway's stage when it's created, like the visit rule. No override: recording the consent is the action.
    /// </summary>
    public bool NeedsConsent { get; set; }
    /// <summary>Copied from the template: whether staff may skip it, with a reason, when it's current.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Copied from the template: whether finishing it chooses one of the branches that follow.</summary>
    public bool ChoosesBranch { get; set; }

    /// <summary>Copied from the template: the branch it belongs to, or null for a stage that always runs.</summary>
    public string? Branch { get; set; }

    /// <summary>
    /// When it was skipped (an optional stage skipped by hand, or another branch chosen), and by whom; null while it
    /// isn't. A skipped stage is neither current nor worked on, and counts as done for the pathway's status, until
    /// it's reinstated (which clears these).
    /// </summary>
    public DateTime? SkippedAtUtc { get; set; }
    public Guid? SkippedByUserId { get; set; }
    public StageSkipReason? SkipReason { get; set; }

    /// <summary>Why, in words, for <see cref="StageSkipReason.Other"/> (optional otherwise). Free text: admins only on the timeline.</summary>
    public string? SkipDetail { get; set; }

    public bool IsSkipped => SkippedAtUtc is not null;
    /// <summary>
    /// Copied from the template: finishing this stage stops the waiting-time clock of the referral the pathway was
    /// accepted from (at most one stage of a template does). A stopped clock never restarts, even if the stage is unlocked.
    /// </summary>
    public bool StopsClock { get; set; }

    /// <summary>The staff member's notes for this stage, editable until it's finished (or while an admin has it unlocked).</summary>
    public string Notes { get; set; } = string.Empty;

    public DateTime? StartedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }

    /// <summary>A finished stage is locked. An admin can unlock it so it can be corrected and finished again.</summary>
    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}
