namespace MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;

/// <summary>Edits a stage definition. Only the fields that are set change. Existing procedures keep their copy.</summary>
public sealed class UpdatePatientProcedureStageDefinitionRequest
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
}
