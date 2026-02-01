namespace MedApp.API.Dtos.Responses;

public class PatientResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? Email { get; set; }

}