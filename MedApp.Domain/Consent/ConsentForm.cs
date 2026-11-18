namespace MedApp.Domain.Consent;

/// <summary>
/// A consent form an organisation uses ("Consent for a skin procedure", version "3"): organisation-wide setup, kept by
/// holders of ManageSetup and read by everyone. One a consent record has used is archived, not deleted. Its name and
/// version are admin-written, and each record keeps its own copy of them.
/// </summary>
public sealed class ConsentForm
{
    public const int MaxNameLength = 200;
    public const int MaxFormVersionLength = 50;

    public Guid Id { get; init; }

    /// <summary>Unique with <see cref="FormVersion"/>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The form's own version or edition, as printed on it ("3", "2026-01").</summary>
    public string FormVersion { get; set; } = string.Empty;

    /// <summary>Archived forms can't be chosen for new records; records already made keep theirs.</summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; set; }
}
