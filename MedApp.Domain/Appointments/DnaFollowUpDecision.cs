namespace MedApp.Domain.Appointments;

/// <summary>
/// What booking staff decided after a patient didn't attend (centre plan decision 132). Recorded by people; MedApp never
/// discharges anyone or changes a pathway because of a missed appointment. Stored by number, so never renumber these.
/// </summary>
public enum DnaFollowUpDecision
{
    /// <summary>Another appointment is to be booked.</summary>
    Rebook = 0,

    /// <summary>A clinician is to look at it and decide.</summary>
    ClinicianToReview = 1,

    /// <summary>No further appointment (decided by whoever may decide it, which the detail says).</summary>
    NoFurtherAppointment = 2
}
