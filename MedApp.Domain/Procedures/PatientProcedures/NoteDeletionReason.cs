namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// Why a procedure's note is deleted, or asked to be: a fixed choice, with an optional detail beside it. Stored as a
/// number, so values never change. <see cref="WrongPatient"/> matters beyond the note: its text is another person's
/// data on this patient's record, so the patient's export flags it for the reviewing clinician to hold back.
/// </summary>
public enum NoteDeletionReason
{
    /// <summary>Written on the wrong patient's record.</summary>
    WrongPatient = 0,

    /// <summary>Written in error (wrong, or not meant to be saved).</summary>
    WrittenInError = 1,

    /// <summary>A duplicate of another note.</summary>
    Duplicate = 2,

    /// <summary>Something else, said in the detail (required then).</summary>
    Other = 3
}
