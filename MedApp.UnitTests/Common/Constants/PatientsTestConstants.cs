using MedApp.Application.Patients.Queries.SearchPatients;
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

    public const int DefaultPage = 1;
    public const int DefaultPageSize = 10;

    /// <summary>A search for the first page with no filters, sorted by name; override what a test needs.</summary>
    public static SearchPatientsQuery CreateSearchQuery() =>
        new(DefaultPage, DefaultPageSize, null, true, null, null, null, null, null);

    /// <summary>A synthetic patient with the given details, for search tests.</summary>
    public static Patient CreatePatient(string firstName, string lastName, DateOnly dateOfBirth, string email)
    {
        return new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dateOfBirth,
            Email = email,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastUpdated = DateTime.UtcNow.AddDays(-1)
        };
    }

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