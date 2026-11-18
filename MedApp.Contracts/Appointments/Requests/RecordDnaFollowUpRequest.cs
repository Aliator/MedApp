namespace MedApp.Contracts.Appointments.Requests;

/// <summary>A Did not attend's follow-up (centre plan decision 132).</summary>
public sealed class RecordDnaFollowUpRequest
{
    /// <summary>Rebook, ClinicianToReview or NoFurtherAppointment.</summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>At most 500 characters; required for NoFurtherAppointment (who decided it, and why).</summary>
    public string? Detail { get; set; }
}
