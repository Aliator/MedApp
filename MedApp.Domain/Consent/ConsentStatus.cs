namespace MedApp.Domain.Consent;

/// <summary>A consent record's status (decision 145). Stored by number, so never renumber it.</summary>
public enum ConsentStatus
{
    /// <summary>Recorded and not withdrawn: what the stage gate looks for.</summary>
    Valid = 0,

    /// <summary>The patient (or whoever consented) withdrew it, on a date, with a detail.</summary>
    Withdrawn = 1,

    /// <summary>It shouldn't have been recorded (the wrong patient, stage or form): a correction, with a detail.</summary>
    EnteredInError = 2
}
