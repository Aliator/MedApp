namespace Domain.Patients;

public sealed class Patient
{
    public Guid Id { get; init; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}