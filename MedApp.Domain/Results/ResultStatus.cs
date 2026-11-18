namespace MedApp.Domain.Results;

/// <summary>
/// Where a result is in its sign-off. Stored as a number, so values never change. AwaitingSignOff and Seen both await
/// sign-off; SignedOff and EnteredInError are final.
/// </summary>
public enum ResultStatus
{
    /// <summary>Reported, not yet seen or signed off.</summary>
    AwaitingSignOff = 0,

    /// <summary>A clinician has read it and action is under way; it still awaits sign-off.</summary>
    Seen = 1,

    /// <summary>A clinician signed it off, recording what was done (<see cref="ResultAction"/>). Final.</summary>
    SignedOff = 2,

    /// <summary>The report was on the wrong patient or specimen: closed, its file deleted. Final.</summary>
    EnteredInError = 3
}

/// <summary>Where a result came from. Stored as a number, so values never change.</summary>
public enum ResultSource
{
    /// <summary>Made when a specimen's report was attached.</summary>
    Specimen = 0,

    /// <summary>An attached file marked as a result from elsewhere (a scan report from another service).</summary>
    ExternalReport = 1
}

/// <summary>
/// What the clinician did when they signed a result off: their own record, chosen by them, listed in this fixed order
/// with none pre-selected. MedApp never suggests one or checks it fits the result. Stored as a number, so values never
/// change.
/// </summary>
public enum ResultAction
{
    NoFurtherAction = 0,
    PatientInformed = 1,
    AppointmentArranged = 2,
    ForMdtDiscussion = 3,
    ReferredOn = 4,

    /// <summary>Anything else: the detail is required.</summary>
    Other = 5
}
