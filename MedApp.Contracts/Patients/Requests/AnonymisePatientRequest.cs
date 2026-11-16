namespace MedApp.Contracts.Patients.Requests;

/// <summary>Body of <c>POST api/patients/{id}/anonymise</c> (admins only).</summary>
public sealed class AnonymisePatientRequest
{
    /// <summary>Why the record is being anonymised (for example, past its retention period); kept in the activity log.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>The patient's full name ("First Last") typed by the admin, to confirm they have the right patient.</summary>
    public string Confirmation { get; set; } = string.Empty;
}
