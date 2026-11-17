namespace MedApp.Contracts.Notepads.Requests;

/// <summary>Saves the signed-in user's notepad.</summary>
public sealed class SaveNotepadRequest
{
    /// <summary>The whole notepad, as Markdown (at most 20,000 characters). Empty clears it.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The notepad's version the page last read or saved (0 for one never saved). If it has been saved elsewhere since
    /// (another tab), the save is refused (409).
    /// </summary>
    public int ExpectedVersion { get; set; }
}
