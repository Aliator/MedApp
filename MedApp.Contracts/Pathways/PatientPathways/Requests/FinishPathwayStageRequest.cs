namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>
/// Optional body for finishing a stage. Needed to finish a stage whose visit is required without an attended
/// appointment, which only an admin can do, with a reason, and to finish a branch point for the first time, naming the
/// branch the clinician chose.
/// </summary>
public sealed class FinishPathwayStageRequest
{
    /// <summary>Finish it without its required visit (admins only).</summary>
    public bool WithoutVisit { get; set; }

    /// <summary>Why it's finished without its visit: required with <see cref="WithoutVisit"/>, at most 500 characters.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// A choice node's outcome, recorded by the clinician (one of the stage's <c>outcomes</c>): required the first time a
    /// choice node is finished, and refused for any other stage or finish. That outcome's stages are added.
    /// </summary>
    public string? Outcome { get; set; }
}
