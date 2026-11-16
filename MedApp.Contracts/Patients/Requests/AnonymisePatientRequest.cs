namespace MedApp.Contracts.Patients.Requests;

/// <summary>Body of <c>POST api/patients/{id}/anonymise</c> (admins only).</summary>
public sealed class AnonymisePatientRequest
{
    /// <summary>Why: RetentionPeriodEnded, ErasureRequest or CreatedInError. Kept in the activity log.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>An optional note for the activity log, which must hold no patient details (it outlives the anonymising).</summary>
    public string? Note { get; set; }

    /// <summary>The patient's full name ("First Last") typed by the admin, to confirm they have the right patient.</summary>
    public string Confirmation { get; set; } = string.Empty;
}
