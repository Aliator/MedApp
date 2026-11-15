namespace MedApp.Contracts.Patients.Requests;

/// <summary>Query string for <c>GET api/patients/search</c>. Every filter is optional.</summary>
public sealed record SearchPatientsRequest
{
    /// <summary>Page number, from 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Rows per page, 1 to 100.</summary>
    public int PageSize { get; init; } = 10;

    /// <summary><c>name</c> (last name, then first name; the default), <c>dateOfBirth</c> or <c>email</c>.</summary>
    public string? Sort { get; init; }

    public bool Ascending { get; init; } = true;

    /// <summary>Matches first names containing this text, ignoring case.</summary>
    public string? FirstName { get; init; }

    /// <summary>Matches last names containing this text, ignoring case.</summary>
    public string? LastName { get; init; }

    /// <summary>Matches emails containing this text, ignoring case.</summary>
    public string? Email { get; init; }

    /// <summary>Born on or after this date.</summary>
    public DateOnly? DateOfBirthFrom { get; init; }

    /// <summary>Born on or before this date.</summary>
    public DateOnly? DateOfBirthTo { get; init; }
}
