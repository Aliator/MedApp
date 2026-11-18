namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>A reusable stage, such as "Consultation", that templates list (possibly more than once).</summary>
public sealed class PatientPathwayStageDefinition
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// The SNOMED CT concept the stage records (its SCTID), or null. Typed by an admin and checked for format only
    /// (<see cref="Terminology.SnomedCt"/>): there's no terminology server.
    /// </summary>
    public string? SnomedCode { get; set; }

    /// <summary>The concept's display term, as the admin typed it; set exactly when <see cref="SnomedCode"/> is.</summary>
    public string? SnomedTerm { get; set; }
    /// <summary>Whether the stage involves a visit; copied onto each pathway's stage when it's created.</summary>
    public VisitRequirement VisitRequirement { get; set; }

    /// <summary>
    /// The kind of letter the stage needs before it can be finished (a "Discharge letter to GP" stage needs a discharge
    /// summary), or null for none; copied onto each pathway's stage when it's created, like the visit rule.
    /// </summary>
    public Letters.LetterKind? NeedsLetterKind { get; set; }

    /// <summary>
    /// Whether the stage waits for results: it can't be finished while any result of the pathway awaits sign-off (a
    /// "Results and team review" stage); copied onto each pathway's stage when it's created.
    /// </summary>
    public bool WaitsForResults { get; set; }

    /// <summary>
    /// Whether the stage needs a Valid consent record before it can be finished (decision 146); copied onto each
    /// pathway's stage when it's created, like the visit rule. No override: recording the consent is the action.
    /// </summary>
    public bool NeedsConsent { get; set; }

    /// <summary>Archived definitions can't be added to templates. A definition in use is archived, never deleted.</summary>
    public bool IsArchived { get; set; }
}
