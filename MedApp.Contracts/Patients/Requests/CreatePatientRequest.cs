namespace MedApp.Contracts.Patients.Requests;

public sealed class CreatePatientRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = "";

    /// <summary>
    /// Optional: ten digits with a valid check digit, with or without spaces. Left out or empty, the patient has none.
    /// </summary>
    public string? NhsNumber { get; set; }
}