namespace MedApp.Contracts.Pathways.PatientPathways.Responses;

public sealed class PatientPathwayStageResponse
{
    public Guid Id { get; set; }
    public Guid StageDefinitionId { get; set; }
    public int StageOrder { get; set; }

    /// <summary>Copied from the stage definition when the pathway was created.</summary>
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    /// <summary>The SNOMED CT code and term, copied from the stage definition when the pathway was created, or null.</summary>
    public string? SnomedCode { get; set; }
    public string? SnomedTerm { get; set; }
    /// <summary>Copied from the stage definition: None, Suggested or Required (a visit before it can be finished).</summary>
    public string VisitRequirement { get; set; } = string.Empty;

    /// <summary>
    /// Copied from the stage definition: the kind of letter (by name) that must be approved and sent to everyone before
    /// the stage can be finished, or null for none.
    /// </summary>
    public string? NeedsLetterKind { get; set; }

    /// <summary>
    /// Copied from the stage definition: whether the stage can't be finished while any of the pathway's results awaits
    /// sign-off.
    /// </summary>
    public bool WaitsForResults { get; set; }

    /// <summary>
    /// Copied from the stage definition: whether a Valid consent record for the stage is needed before it can be finished
    /// (no override: recording the consent is the action).
    /// </summary>
    public bool NeedsConsent { get; set; }

    /// <summary>
    /// Whether the viewer can record consent to it now: they hold RecordConsent, the record can change, the pathway is
    /// open and the stage is neither finished nor skipped.
    /// </summary>
    public bool CanRecordConsent { get; set; }

    public string Notes { get; set; } = string.Empty;

    /// <summary>Copied from the template: whether it can be skipped, with a reason, while it's current.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Copied from the template: a choice node's kind (Assessment or LabResult), or null for a plain stage.</summary>
    public string? ChoiceKind { get; set; }

    /// <summary>
    /// A choice node's outcomes, in the template's order, each with the stages recording it would add; empty for any
    /// other stage. None is chosen or pre-selected: the clinician records one when finishing it.
    /// </summary>
    public List<PatientPathwayOutcomeResponse> Outcomes { get; set; } = [];

    /// <summary>A choice node's recorded outcome, or null until it's recorded (and for a plain stage).</summary>
    public string? Outcome { get; set; }

    /// <summary>Whether finishing it now asks for an outcome: a choice node not yet recorded.</summary>
    public bool NeedsOutcome { get; set; }

    /// <summary>The results the lab entered on its own stage (free text), or null.</summary>
    public PatientPathwayStageResultResponse? Result { get; set; }

    /// <summary>A lab stage's lab case (its open one, else its latest), for its page to link to; null until the lab has cases.</summary>
    public Guid? LabCaseId { get; set; }
    public string? LabCaseNumber { get; set; }
    public string? LabCaseStatus { get; set; }

    /// <summary>The stages straight before it (MC8): a join has several; the first stage none.</summary>
    public List<Guid> PredecessorIds { get; set; } = [];

    /// <summary>The added branch (MC10) it came with, or null for a stage from the template.</summary>
    public Guid? BranchAdditionId { get; set; }

    /// <summary>NotStarted, InProgress, Completed or Skipped.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>The stage being worked on now.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>A finished stage is locked until an admin unlocks it.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether its notes can be edited and it can be finished now.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether the viewer can unlock it now (UnlockStages): it's finished and locked, and the pathway isn't blocked, cancelled or archived.</summary>
    public bool CanUnlock { get; set; }

    /// <summary>Whether the viewer can skip it now: it's optional and the current stage of an open pathway.</summary>
    public bool CanSkip { get; set; }

    /// <summary>Whether the viewer can reinstate it now (UnlockStages): it's skipped, and the pathway is open or discharged.</summary>
    public bool CanReinstate { get; set; }

    /// <summary>When it was skipped, by whom, and why (NotNeeded, PatientDeclined, Other or BranchNotChosen); null unless skipped.</summary>
    public DateTime? SkippedAtUtc { get; set; }
    public Guid? SkippedByUserId { get; set; }
    public string? SkippedByUsername { get; set; }
    public string? SkipReason { get; set; }

    /// <summary>The detail given with the skip reason (free text, shown on the pathway page as notes are).</summary>
    public string? SkipDetail { get; set; }

    /// <summary>
    /// Whether the viewer can record a specimen taken at it now: they hold RecordSpecimens, the stage can be edited, the
    /// pathway is open (not on hold or closed) and the record can change.
    /// </summary>
    public bool CanRecordSpecimen { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }
    public string? StartedByUsername { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? CompletedByUsername { get; set; }

    /// <summary>The team that works it (MC7, the relay): null for the pathway's own team; the lab for its stages.</summary>
    public Guid? OwnerTeamId { get; set; }
    public string? OwnerTeamName { get; set; }

    /// <summary>Whether it's current and with another team than the pathway's, which the viewer's team doesn't work.</summary>
    public bool IsWithAnotherTeam { get; set; }

    /// <summary>When it first became current.</summary>
    public DateTime? ReachedAtUtc { get; set; }

    /// <summary>The team that handed it over, when it came from another team's stage; else null.</summary>
    public string? HandedOverFromTeamName { get; set; }

    /// <summary>When a handed-over stage was picked up, and by whom.</summary>
    public DateTime? PickedUpAtUtc { get; set; }
    public Guid? PickedUpByUserId { get; set; }
    public string? PickedUpByUsername { get; set; }

    /// <summary>Whether the viewer can pick it up now: handed to their team, current, and not picked up yet.</summary>
    public bool CanPickUp { get; set; }
}

/// <summary>One outcome of a choice node: its name, and the names of the stages recording it would add.</summary>
public sealed class PatientPathwayOutcomeResponse
{
    public string Name { get; set; } = string.Empty;
    public List<string> StageNames { get; set; } = [];
}

/// <summary>The results the lab entered on its stage (free text), with who entered them and when.</summary>
public sealed class PatientPathwayStageResultResponse
{
    public string Text { get; set; } = string.Empty;
    public string? EnteredByUsername { get; set; }
    public DateTime? EnteredAtUtc { get; set; }
}
