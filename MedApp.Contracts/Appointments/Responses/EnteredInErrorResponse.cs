namespace MedApp.Contracts.Appointments.Responses;

/// <summary>An appointment withdrawn as entered in error, and what happened to its stage's visit.</summary>
public sealed class EnteredInErrorResponse
{
    public AppointmentResponse Appointment { get; set; } = new();

    /// <summary>The stage's visit was asked for again (it's back in Requires Appointment).</summary>
    public bool VisitRequestedAgain { get; set; }

    /// <summary>What happened to the stage's visit, or why it wasn't asked for again; null when it wasn't for a stage with a visit.</summary>
    public string? StageMessage { get; set; }
}
