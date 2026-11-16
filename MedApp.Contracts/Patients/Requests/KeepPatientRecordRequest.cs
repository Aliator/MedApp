namespace MedApp.Contracts.Patients.Requests;

/// <summary>Body of <c>POST api/patients/{id}/retention/keep</c> (admins only): keep a record past its retention period.</summary>
public sealed class KeepPatientRecordRequest
{
    /// <summary>When to review it again: after today, and no more than the configured maximum (2 years) ahead.</summary>
    public DateOnly KeepUntil { get; set; }

    /// <summary>Why it's being kept, for example a legal hold or ongoing care.</summary>
    public string Reason { get; set; } = string.Empty;
}
