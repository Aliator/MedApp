namespace MedApp.Domain.Specimens;

/// <summary>
/// Where a specimen is. Moves forward only: Taken, Sent, Received, Reported; or, from any open one (Taken, Sent or
/// Received), RejectedByLab, Lost or EnteredInError, which close it. Stored as a number, so values never change.
/// </summary>
public enum SpecimenStatus
{
    /// <summary>Taken from the patient at a stage, not yet sent to the lab.</summary>
    Taken = 0,

    /// <summary>On its way to the lab.</summary>
    Sent = 1,

    /// <summary>The lab has it.</summary>
    Received = 2,

    /// <summary>The lab's report is attached (a file). Closed.</summary>
    Reported = 3,

    /// <summary>The lab couldn't use it (<see cref="SpecimenRejectionReason"/>). Closed: taking another is a new specimen.</summary>
    RejectedByLab = 4,

    /// <summary>It went missing. Closed: taking another is a new specimen.</summary>
    Lost = 5,

    /// <summary>Recorded by mistake (the wrong patient, recorded twice). Closed.</summary>
    EnteredInError = 6
}
