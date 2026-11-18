namespace MedApp.Contracts.Activity.Responses;

/// <summary>One entry in the activity log.</summary>
public sealed class ActivityEntryResponse
{
    public Guid Id { get; set; }
    public DateTime AtUtc { get; set; }

    /// <summary>What happened, e.g. PatientViewed, SignInFailed or RolesChanged.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Who did it; null only for a refused sign-in to an account that doesn't exist.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Their username at the time; for a refused sign-in, the one that was tried.</summary>
    public string Username { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public Guid? PatientId { get; set; }

    /// <summary>The patient's name as it is now ("Deleted patient" if they've been deleted).</summary>
    public string? PatientName { get; set; }

    public Guid? PathwayId { get; set; }

    /// <summary>The account it's about, for account and role changes.</summary>
    public string? TargetUsername { get; set; }

    /// <summary>More about it, as JSON: the fields changed (never their values), a reason, roles, and so on.</summary>
    public string Details { get; set; } = "{}";
}
