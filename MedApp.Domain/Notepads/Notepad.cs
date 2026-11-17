namespace MedApp.Domain.Notepads;

/// <summary>
/// A member of staff's own notepad: one per account, Markdown text kept on the server (never in the browser, as it may
/// hold patient details). It isn't linked to any patient, so a patient's export and anonymisation can't find it: it's
/// cleared after <c>Notepad:KeepDays</c> without a change, when it's next read or saved, and goes with the account.
/// </summary>
public sealed class Notepad
{
    /// <summary>The longest a notepad's text can be.</summary>
    public const int MaxTextLength = 20000;

    /// <summary>Its owner's account ID (the key: one notepad each).</summary>
    public Guid UserId { get; init; }

    public string Text { get; set; } = string.Empty;

    public DateTime LastUpdatedUtc { get; set; }

    /// <summary>One more for each save; the page sends back the one it last saw, so a save from a stale tab is refused (409).</summary>
    public int Version { get; set; }

    /// <summary>A concurrency token: a save from a copy read before another save is refused (409).</summary>
    public byte[] RowVersion { get; set; } = [];
}
