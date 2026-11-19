namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// The kind of a choice node (MC10b): a stage whose finish records the clinician's outcome, which decides the stages
/// that follow. Stored as a number, so values never change. MedApp never chooses or suggests an outcome.
/// </summary>
public enum StageChoiceKind
{
    /// <summary>A clinician's assessment or review.</summary>
    Assessment = 0,

    /// <summary>A lab result coming back to the clinic.</summary>
    LabResult = 1
}
