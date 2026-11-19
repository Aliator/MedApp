namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// One change to a pathway: who did what, and when. Written in the same save as the change. It has no foreign
/// key to the pathway, so a deleted pathway's history is kept; the pathway's title and patient are copied in.
/// </summary>
public sealed class PatientPathwayAuditEntry
{
    /// <summary>
    /// The <see cref="PatientPathwayId"/> of an entry about the patient rather than one of their pathways: a file
    /// attached to the patient themselves. Such entries have no pathway title or stage.
    /// </summary>
    public static readonly Guid NoPathway = Guid.Empty;

    public Guid Id { get; init; }

    /// <summary>The pathway it's about, or <see cref="NoPathway"/> for an entry about the patient's own file.</summary>
    public Guid PatientPathwayId { get; init; }

    /// <summary>The pathway's title at the time, so the entry still reads well after the pathway is deleted.</summary>
    public string PathwayTitle { get; set; } = string.Empty;

    public Guid PatientId { get; init; }

    /// <summary>
    /// The team it belongs to (the team walls, decision 200 of <c>docs/multi-clinic-plan.md</c>): copied from what it's about (its pathway, file, letter, result, consent record or specimen), for the timeline's filter. Set when
    /// it's made, never edited by hand.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>The stage the change was about, if any.</summary>
    public Guid? StageId { get; init; }

    /// <summary>The stage's name at the time, if any.</summary>
    public string? StageName { get; init; }

    public PatientPathwayAuditAction Action { get; init; }

    public Guid UserId { get; init; }

    /// <summary>The username at the time, kept even if the account is later deleted.</summary>
    public string Username { get; init; } = string.Empty;

    public DateTime AtUtc { get; init; }

    /// <summary>What changed, as JSON: from/to values, the notes text, a reason, and so on.</summary>
    public string Details { get; set; } = "{}";
}
