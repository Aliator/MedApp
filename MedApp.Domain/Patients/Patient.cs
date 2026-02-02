namespace MedApp.Domain.Patients;

public sealed class Patient
{
    public Guid Id { get; init; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}