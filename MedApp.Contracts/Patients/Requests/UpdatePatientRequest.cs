namespace MedApp.Contracts.Patients.Requests;

public sealed class UpdatePatientRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = "";

    /// <summary>
    /// Ten digits with a valid check digit, with or without spaces. Left out (null), it's unchanged; empty, it's
    /// removed.
    /// </summary>
    public string? NhsNumber { get; set; }
}