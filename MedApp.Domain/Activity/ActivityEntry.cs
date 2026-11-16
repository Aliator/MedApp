namespace MedApp.Domain.Activity;

/// <summary>
/// One entry in the activity log: who read or changed patient data, signed in or out, or changed an account, when,
/// and from which address. It has no foreign keys, so it outlives the patient and the account it's about. It holds
/// IDs, never a patient's name or other details, so anonymising a patient leaves nothing identifying here.
/// </summary>
public sealed class ActivityEntry
{
    public Guid Id { get; init; }

    public DateTime AtUtc { get; init; }

    public ActivityAction Action { get; init; }

    /// <summary>Who did it. Null only for a refused sign-in to an account that doesn't exist.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Their username at the time; for a refused sign-in, the username that was tried.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>The address the request came from, as the API saw it.</summary>
    public string? IpAddress { get; init; }

    /// <summary>The patient it's about, if any.</summary>
    public Guid? PatientId { get; init; }

    /// <summary>The procedure it's about, if any.</summary>
    public Guid? ProcedureId { get; init; }

    /// <summary>The account it's about, for account and role changes.</summary>
    public string? TargetUsername { get; init; }

    /// <summary>More about it, as JSON: the fields changed (never their values), a reason, the roles, and so on.</summary>
    public string Details { get; set; } = "{}";
}
