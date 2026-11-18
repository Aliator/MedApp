namespace MedApp.Contracts.Roles.Responses;

/// <summary>A permission in the catalogue: what it allows, and where the roles page shows it.</summary>
public sealed class PermissionResponse
{
    /// <summary>Its name, as roles and <c>whoami</c> list it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The group the page shows it in: <c>Base</c> (<c>UseApp</c> alone, which every role has), <c>Patients</c>,
    /// <c>Referrals</c>, <c>Pathways</c>, <c>AppointmentsAndCalendars</c>, <c>Files</c>, <c>Oversight</c>, <c>Setup</c>
    /// or <c>AdministratorOnly</c>.
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>What it allows, in plain words.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Only the built-in Admin role has it; any other role is refused it.</summary>
    public bool IsAdminOnly { get; set; }
}
