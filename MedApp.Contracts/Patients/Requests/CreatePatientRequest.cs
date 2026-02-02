namespace MedApp.Contracts.Patients.Requests;

public sealed class CreatePatientRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Email { get; set; }
}
