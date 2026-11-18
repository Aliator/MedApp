namespace MedApp.Contracts.Clinics.Responses;

/// <summary>What Create sessions made from a pattern, and the dates it skipped, with why.</summary>
public sealed class CreateSessionsResponse
{
    public List<ClinicSessionResponse> Created { get; set; } = [];

    public List<SkippedSessionResponse> Skipped { get; set; } = [];
}

/// <summary>A date a pattern would have made a session on, and why it didn't.</summary>
public sealed class SkippedSessionResponse
{
    public DateOnly Date { get; set; }

    public string Reason { get; set; } = string.Empty;
}
