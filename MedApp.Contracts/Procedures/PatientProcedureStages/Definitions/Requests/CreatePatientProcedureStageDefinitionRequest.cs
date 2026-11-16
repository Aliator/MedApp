namespace MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;

public sealed class CreatePatientProcedureStageDefinitionRequest
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
}