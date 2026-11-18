namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>
/// Edits a pathway's details. Only the fields that are set change; stages come from the template and don't, and notes
/// are added and edited one at a time (<c>.../notes</c>).
/// </summary>
public sealed class UpdatePatientPathwayRequest
{
    public string? Title { get; set; }

    /// <summary>Due by the end of this day, UK time.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Low, Medium, High or Urgent.</summary>
    public string? Priority { get; set; }

    /// <summary>
    /// No longer accepted: anything but null is a 400 saying notes are now added on the pathway's page. It's kept, for
    /// one release, so a page loaded before notes became a list is told, rather than having its change silently dropped.
    /// </summary>
    public string? Notes { get; set; }
}
