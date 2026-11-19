namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>Adds one of the template's addable branches to a pathway (MC10), after one of its stages, with a reason.</summary>
public sealed class AddPathwayBranchRequest
{
    public Guid TemplateBranchId { get; set; }

    /// <summary>The stage it comes after.</summary>
    public Guid AfterStageId { get; set; }

    /// <summary>Why it's being added (required, at most 500 characters): shown on the pathway, never in the audit log.</summary>
    public string? Reason { get; set; }
}
