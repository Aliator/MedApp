namespace MedApp.Contracts.Clinics.Responses;

/// <summary>A cancelled session, and the patients whose appointments were cancelled with it: the list of whom to tell.</summary>
public sealed class CancelClinicSessionResponse
{
    public ClinicSessionResponse Session { get; set; } = new();

    public List<PatientToTellResponse> PatientsToTell { get; set; } = [];
}

/// <summary>A patient whose appointment was cancelled with its session.</summary>
public sealed class PatientToTellResponse
{
    public Guid AppointmentId { get; set; }

    public Guid PatientId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string? NhsNumber { get; set; }

    public DateTime StartUtc { get; set; }
}
