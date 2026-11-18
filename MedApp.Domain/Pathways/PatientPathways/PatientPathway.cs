using MedApp.Domain.Patients;

namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// One patient going through a pathway template: a copy of the template's stages, worked through in order.
/// </summary>
public sealed class PatientPathway
{
    public Guid Id { get; init; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The old single notes field, no longer used: its text was moved to <see cref="Notes"/> as one note each. Kept
    /// (stored in the <c>Notes</c> column) only so API copies from before the move still work during a rollout; dropped
    /// in a later release. Anonymisation still clears it.
    /// </summary>
    public string LegacyNotes { get; set; } = string.Empty;

    public DateTime DueDateUtc { get; set; }

    public PatientPathwayPriority Priority { get; set; }

    public PatientPathwayStatus Status { get; set; }

    /// <summary>The status a Blocked pathway goes back to when it's unblocked.</summary>
    public PatientPathwayStatus? BlockedFromStatus { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The template it was created from (null for pathways created before templates were required).</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>The template's name when the pathway was created, kept even if the template is renamed or archived.</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Its template's specialty when it was created, copied as the stages are, so moving the template later doesn't
    /// move old pathways in lists and reports. Pathways from before specialties are in General.
    /// </summary>
    public Guid SpecialtyId { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }

    public List<PatientPathwayStage> Stages { get; set; } = [];
    public List<PatientPathwayAssignment> Assignments { get; set; } = [];

    /// <summary>Its notes (see <see cref="PatientPathwayNote"/>).</summary>
    public List<PatientPathwayNote> Notes { get; set; } = [];
}
