namespace MedApp.Domain.Specimens;

/// <summary>
/// A lab specimens are sent to: the centre's own (which uses MedApp itself to receive and report them) or a partner.
/// Organisation-wide setup, kept by holders of ManageSetup; one in use is archived, not deleted.
/// </summary>
public sealed class Laboratory
{
    public const int MaxNameLength = 200;

    public Guid Id { get; init; }

    /// <summary>Unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the lab's own staff use MedApp to receive and report specimens; otherwise the centre's staff record what
    /// the lab tells them. Shown only: it changes no rule.
    /// </summary>
    public bool UsesMedApp { get; set; }

    /// <summary>Archived labs can't be chosen for new specimens; specimens already sent to one keep it.</summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; set; }
}
