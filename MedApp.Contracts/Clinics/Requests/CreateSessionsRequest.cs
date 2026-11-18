namespace MedApp.Contracts.Clinics.Requests;

/// <summary>Makes a pattern's sessions for the next 1 to 13 weeks.</summary>
public sealed class CreateSessionsRequest
{
    public int Weeks { get; set; } = 6;
}
