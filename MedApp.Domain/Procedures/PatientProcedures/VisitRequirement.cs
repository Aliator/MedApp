namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// Whether a stage involves the patient visiting: the app then asks for an appointment when it becomes current.
/// Stored as its number, so never renumber these.
/// </summary>
public enum VisitRequirement
{
    /// <summary>No visit (the default).</summary>
    None = 0,

    /// <summary>A visit is expected: finishing without an attended one asks for confirmation.</summary>
    Suggested = 1,

    /// <summary>The stage can't be finished without an attended visit, except by an admin, with a reason.</summary>
    Required = 2
}
