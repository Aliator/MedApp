namespace MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Requests;

public sealed class CreatePatientPathwayStageDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Optional: the SNOMED CT concept the stage records (its SCTID, 6 to 18 digits). Needs <see cref="SnomedTerm"/>.</summary>
    public string? SnomedCode { get; set; }

    /// <summary>The concept's display term, as the admin types it. Given exactly when <see cref="SnomedCode"/> is.</summary>
    public string? SnomedTerm { get; set; }
    /// <summary>Whether the stage involves a visit: None (the default), Suggested or Required.</summary>
    public string VisitRequirement { get; set; } = "None";

    /// <summary>
    /// The kind of letter the stage needs before it can be finished (<c>DischargeSummary</c>, ...), or null (or
    /// <c>None</c>) for none.
    /// </summary>
    public string? NeedsLetterKind { get; set; }

    /// <summary>Whether the stage waits for results: it can't be finished while any of the pathway's results awaits sign-off.</summary>
    public bool WaitsForResults { get; set; }

    /// <summary>Whether the stage needs a Valid consent record before it can be finished (false by default).</summary>
    public bool NeedsConsent { get; set; }
}