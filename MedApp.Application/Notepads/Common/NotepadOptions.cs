namespace MedApp.Application.Notepads.Common;

/// <summary>The notepad's settings, from the <c>Notepad</c> section of the API's configuration.</summary>
public sealed class NotepadOptions
{
    public const string Section = "Notepad";

    /// <summary>
    /// Days a notepad is kept without a change before it's cleared (when it's next read or saved; nothing runs in the
    /// background). The governance pack's retention schedule records the choice: a notepad is for jottings, and
    /// anything about a patient belongs in their record.
    /// </summary>
    public int KeepDays { get; set; } = 30;

    /// <summary>Said when <see cref="KeepDays"/> isn't at least a day, which stops the API at startup.</summary>
    public const string InvalidKeepDays = "Notepad:KeepDays must be at least 1.";

    /// <summary>Whether the settings can be used: a notepad is kept for at least a day.</summary>
    public bool IsValid => KeepDays >= 1;
}
