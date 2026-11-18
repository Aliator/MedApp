namespace MedApp.Domain.Clinics;

/// <summary>A clinic session's status. Stored as its number, so never renumber it (centre plan, section 5).</summary>
public enum ClinicSessionStatus
{
    /// <summary>On the calendar, taking bookings into its slots.</summary>
    Planned = 0,

    /// <summary>Cancelled with a reason; its booked appointments were cancelled with it. Kept, never deleted.</summary>
    Cancelled = 1
}
