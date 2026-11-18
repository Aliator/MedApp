namespace MedApp.Contracts.Authentication.Requests;

/// <summary>
/// Someone's full name and job title, as they go on letters: <c>PUT api/auth/me/details</c> (your own) or
/// <c>PUT api/auth/users/{username}/details</c> (holders of ManageUsers). Both replace what's there: empty or left out
/// removes it.
/// </summary>
public sealed class StaffDetailsRequest
{
    /// <summary>As it goes on letters ("Dr Sam Patel"): at most 100 characters.</summary>
    public string? FullName { get; set; }

    /// <summary>As it goes on letters ("Consultant dermatologist"): at most 100 characters.</summary>
    public string? JobTitle { get; set; }
}
