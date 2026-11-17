namespace MedApp.Contracts.Notepads.Responses;

/// <summary>The signed-in user's notepad.</summary>
public sealed class NotepadResponse
{
    /// <summary>The text, as Markdown; empty when there's none, or it was cleared after days without a change.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>When it was last saved; null when it never has been (or was cleared).</summary>
    public DateTime? LastUpdatedUtc { get; set; }

    /// <summary>Its version, to send back as <c>expectedVersion</c> with the next save (0 when it has none).</summary>
    public int Version { get; set; }

    /// <summary>How many days without a change it's kept before it's cleared.</summary>
    public int KeepDays { get; set; }
}
