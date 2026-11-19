using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;

namespace MedApp.Domain.Consent;

/// <summary>
/// A record that a patient consented to a stage of their pathway (decisions 145 to 147): the form, the date it was
/// signed, who signed it, who recorded it, and the scanned form (an attachment on the pathway, optional). Its history is
/// in the pathway audit log (<see cref="PatientPathwayAuditAction.ConsentRecorded"/> and on), whose details never hold
/// its free text. <b>MedApp records what the clinician states; it never assesses capacity, validity or whether the form
/// fits.</b>
/// <para>Patient data: the withdrawal or correction detail is free text, shown on the pathway page and to admins on
/// the timeline only.</para>
/// </summary>
public sealed class ConsentRecord
{
    public const int MaxDetailLength = 500;

    public Guid Id { get; init; }

    public Guid PatientId { get; init; }

    /// <summary>
    /// The team it belongs to (the team walls, decision 200 of <c>docs/multi-clinic-plan.md</c>): its pathway's. Set when
    /// it's made, never edited by hand.
    /// </summary>
    public Guid TeamId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid PathwayId { get; init; }
    public PatientPathway Pathway { get; set; } = null!;

    /// <summary>
    /// The stage consented to. No foreign key: a pathway's stages go only with the pathway, which can't be deleted while
    /// it has consent records.
    /// </summary>
    public Guid StageId { get; init; }

    public Guid ConsentFormId { get; init; }
    public ConsentForm ConsentForm { get; set; } = null!;

    /// <summary>The form's name and version when it was recorded (the form may be renamed later).</summary>
    public string FormName { get; init; } = string.Empty;
    public string FormVersion { get; init; } = string.Empty;

    /// <summary>The UK date the form was signed (or the best-interests decision made).</summary>
    public DateOnly SignedOn { get; init; }

    public ConsentSignedBy SignedBy { get; init; }

    /// <summary>The clinician who recorded it (holding RecordConsent), and when.</summary>
    public Guid RecordedByUserId { get; init; }
    public DateTime RecordedAtUtc { get; init; }

    /// <summary>
    /// The scanned, signed form: an attachment on the pathway (or one of its stages), or null ("No scan"). No foreign key,
    /// as attachments have none. It can't be deleted on its own (decision 71).
    /// </summary>
    public Guid? ScanAttachmentId { get; set; }

    public ConsentStatus Status { get; set; }

    /// <summary>Withdrawn: the UK date the consent was withdrawn.</summary>
    public DateOnly? WithdrawnOn { get; set; }

    /// <summary>Withdrawn or entered in error: what happened. Free text.</summary>
    public string? StatusDetail { get; set; }

    public DateTime? StatusChangedAtUtc { get; set; }
    public Guid? StatusChangedByUserId { get; set; }

    /// <summary>One more for each change. A change names the version it started from (409 if it's changed since).</summary>
    public int Version { get; set; }

    /// <summary>The database's row version, a concurrency token: two saves at once can't both win.</summary>
    public byte[] RowVersion { get; set; } = [];
}
