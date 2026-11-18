namespace MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Requests;

/// <summary>Edits a stage definition. Only the fields that are set change. Existing pathways keep their copy.</summary>
public sealed class UpdatePatientPathwayStageDefinitionRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }

    /// <summary>
    /// The SNOMED CT concept, with <see cref="SnomedTerm"/>: both change together. Left out (null), both stay as they
    /// are; empty, both are removed.
    /// </summary>
    public string? SnomedCode { get; set; }

    /// <summary>The concept's display term, given with <see cref="SnomedCode"/>.</summary>
    public string? SnomedTerm { get; set; }
    /// <summary>None, Suggested or Required. Pathways already created keep their stages' copy.</summary>
    public string? VisitRequirement { get; set; }

    /// <summary>
    /// The kind of letter the stage needs before it can be finished, <c>None</c> for none, or null to leave it as it is.
    /// Pathways already created keep their stages' copy.
    /// </summary>
    public string? NeedsLetterKind { get; set; }
}
