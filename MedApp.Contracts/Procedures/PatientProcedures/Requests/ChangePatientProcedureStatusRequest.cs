namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

/// <summary>A manual status change. The procedure's allowedStatusChanges lists what's possible.</summary>
public sealed class ChangePatientProcedureStatusRequest
{
    public string Status { get; set; } = string.Empty;

    /// <summary>Required when blocking or cancelling.</summary>
    public string? Reason { get; set; }
}
