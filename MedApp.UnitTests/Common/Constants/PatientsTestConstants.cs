using MedApp.Domain.Patients;

namespace MedApp.UnitTests.Common.Constants;

public static class PatientsTestConstants
{
    public const string ValidFirstName = "FirstName";
    public const string ValidLastName = "LastName";
    public const string ValidEmail = "test@test.com";
    public const string InvalidEmail = "test";

    public static readonly DateOnly ValidDateOfBirth = new(1111, 1, 1);

    public const int MaxNameLength = 100;

    public static Patient CreateValidPatient()
    {
        return new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = ValidFirstName,
            LastName = ValidLastName,
            DateOfBirth = ValidDateOfBirth,
            Email = ValidEmail,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastUpdated = DateTime.UtcNow.AddDays(-1)
        };
    }
}