namespace MedApp.API.Dtos.Requests;

public class CreatePatientRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? Email { get; set; }
}
