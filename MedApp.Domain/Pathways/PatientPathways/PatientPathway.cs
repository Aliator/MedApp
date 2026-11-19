using MedApp.Domain.Patients;
using MedApp.Domain.Referrals;

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
    /// Its template's team when it was created, copied as the stages are, so moving the template later doesn't
    /// move old pathways in lists and reports. Pathways from before teams are in General.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// Its template's retention class, copied when it's created, and raised (never lowered) when the template is given a
    /// longer one, so a class set on a template after the fact covers the records it was already used for. A patient's record is kept for the
    /// longest of the adult or child period and each of their pathways' classes.
    /// </summary>
    public RetentionClass RetentionClass { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }

    public List<PatientPathwayStage> Stages { get; set; } = [];
    public List<PatientPathwayAssignment> Assignments { get; set; } = [];

    /// <summary>How its stages follow each other (MC8): the template's links among the stages it has, and any added branch's.</summary>
    public List<PatientPathwayStageLink> Links { get; set; } = [];

    /// <summary>
    /// Its template's whole tree, kept when it was made (MC10b): it grows from this as outcomes are recorded. Null for a
    /// pathway made before plans (it has every stage already). Stored as JSON; replaced, never changed in place.
    /// </summary>
    public PathwayPlan? Plan { get; set; }

    /// <summary>The template's addable branches added to it (MC10), with their reasons.</summary>
    public List<PathwayBranchAddition> BranchAdditions { get; set; } = [];

    /// <summary>Its team (loaded with it, for its name).</summary>
    public Teams.Team? Team { get; set; }

    /// <summary>Its notes (see <see cref="PatientPathwayNote"/>).</summary>
    public List<PatientPathwayNote> Notes { get; set; } = [];

    /// <summary>
    /// The referral it was accepted from, if any: its waiting-time clock sets the pathway's target date while it runs
    /// or is paused, and stops with the pathway (<c>PathwayChange</c>). Null for a pathway started without a referral.
    /// </summary>
    public Referral? Referral { get; set; }
}
