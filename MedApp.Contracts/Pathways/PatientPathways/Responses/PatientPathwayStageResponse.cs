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

    public string Notes { get; set; } = string.Empty;

    /// <summary>Copied from the template: whether it can be skipped, with a reason, while it's current.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Copied from the template: whether it's a branch point (finishing it chooses one of <see cref="Branches"/>).</summary>
    public bool ChoosesBranch { get; set; }

    /// <summary>Copied from the template: the branch it belongs to, or null for a stage that always runs.</summary>
    public string? Branch { get; set; }

    /// <summary>
    /// A branch point's branches, in the template's order, each with its stages' names; empty for any other stage.
    /// None is chosen or pre-selected: the clinician chooses when finishing it.
    /// </summary>
    public List<PatientPathwayBranchResponse> Branches { get; set; } = [];

    /// <summary>Whether finishing it now asks for a branch: a branch point being finished for the first time.</summary>
    public bool NeedsBranchChoice { get; set; }

    /// <summary>NotStarted, InProgress, Completed or Skipped.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>The stage being worked on now.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>A finished stage is locked until an admin unlocks it.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether its notes can be edited and it can be finished now.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether an admin can unlock it now: it's finished and locked, and the pathway isn't blocked, cancelled or archived.</summary>
    public bool CanUnlock { get; set; }

    /// <summary>Whether the viewer can skip it now: it's optional and the current stage of an open pathway.</summary>
    public bool CanSkip { get; set; }

    /// <summary>Whether the viewer can reinstate it now (OverrideStages): it's skipped, and the pathway is open or discharged.</summary>
    public bool CanReinstate { get; set; }

    /// <summary>When it was skipped, by whom, and why (NotNeeded, PatientDeclined, Other or BranchNotChosen); null unless skipped.</summary>
    public DateTime? SkippedAtUtc { get; set; }
    public Guid? SkippedByUserId { get; set; }
    public string? SkippedByUsername { get; set; }
    public string? SkipReason { get; set; }

    /// <summary>The detail given with the skip reason (free text, shown on the pathway page as notes are).</summary>
    public string? SkipDetail { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }
    public string? StartedByUsername { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? CompletedByUsername { get; set; }
}

/// <summary>One branch after a branch point: its name and its stages' names, in order.</summary>
public sealed class PatientPathwayBranchResponse
{
    public string Name { get; set; } = string.Empty;
    public List<string> StageNames { get; set; } = [];

    /// <summary>Its stages still to do (neither finished nor skipped): what choosing another branch skips.</summary>
    public List<string> StageNamesToDo { get; set; } = [];
}
