namespace MedApp.Contracts.Clinics.Responses;

/// <summary>A clinic session with its slots, and who is booked into each (reading it is logged as listing them).</summary>
public sealed class ClinicSessionDetailResponse
{
    public ClinicSessionResponse Session { get; set; } = new();

    public List<ClinicSlotResponse> Slots { get; set; } = [];

    /// <summary>Once it's cancelled: the patients whose appointments were cancelled with it, to tell (the call list again).</summary>
    public List<PatientToTellResponse> PatientsToTell { get; set; } = [];
}

/// <summary>One slot of a session: free, or the appointment in it.</summary>
public sealed class ClinicSlotResponse
{
    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>It has started.</summary>
    public bool IsPast { get; set; }

    public Guid? AppointmentId { get; set; }

    public Guid? PatientId { get; set; }

    public string? PatientName { get; set; }

    /// <summary>The appointment's: Booked, Attended or DidNotAttend.</summary>
    public string? AppointmentStatus { get; set; }
}
