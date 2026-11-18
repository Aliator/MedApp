namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>
/// Optional body for finishing a stage. Only needed to finish a stage whose visit is required without an attended
/// appointment, which only an admin can do, with a reason.
/// </summary>
public sealed class FinishPathwayStageRequest
{
    /// <summary>Finish it without its required visit (admins only).</summary>
    public bool WithoutVisit { get; set; }

    /// <summary>Why it's finished without its visit: required with <see cref="WithoutVisit"/>, at most 500 characters.</summary>
    public string? Reason { get; set; }
}
