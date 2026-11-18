namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>Adds a note to a pathway, by the signed-in user.</summary>
public sealed class AddPathwayNoteRequest
{
    /// <summary>The note, as Markdown (at most 4,000 characters).</summary>
    public string Text { get; set; } = string.Empty;
}
