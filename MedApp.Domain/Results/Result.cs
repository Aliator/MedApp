using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;

namespace MedApp.Domain.Results;

/// <summary>
/// A report a named clinician must sign off (<c>docs/centre-phases-3-5-plan.md</c>, decisions 91 to 97): made when a
/// specimen's report is attached, or from an attached file marked as a result from elsewhere. The report is the file
/// (<see cref="ReportAttachmentId"/>), shown as it came: <b>MedApp never reads, interprets or flags its contents</b>. A
/// clinician marks it seen (optional) and signs it off with what they did, in their own words. Its history is in the
/// pathway audit log (<see cref="PatientPathwayAuditAction.ResultRecorded"/> to
/// <see cref="PatientPathwayAuditAction.ResultEnteredInError"/>), whose details never hold its free text.
/// <para>
/// Patient data: the title, action detail and error detail are free text, shown on its page, never on a staff timeline,
/// in a notification or a log.
/// </para>
/// </summary>
public sealed class Result
{
    public const int MaxTitleLength = 200;
    public const int MaxActionDetailLength = 1000;
    public const int MaxErrorDetailLength = 500;

    public Guid Id { get; init; }

    public Guid PatientId { get; init; }

    /// <summary>
    /// The team it belongs to (the team walls, decision 200 of <c>docs/multi-clinic-plan.md</c>): its pathway's, else the recorder's team. Set when
    /// it's made, never edited by hand.
    /// </summary>
    public Guid TeamId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The pathway it belongs to, if any (an external result may be the patient's alone).</summary>
    public Guid? PathwayId { get; init; }
    public PatientPathway? Pathway { get; set; }

    /// <summary>The specimen whose report it is, if it came from one.</summary>
    public Guid? SpecimenId { get; init; }
    public Specimen? Specimen { get; set; }

    public ResultSource Source { get; init; }

    /// <summary>
    /// What it is: for a specimen's, its type and number ("Skin biopsy (punch) MA26-000123"); for an external one, what
    /// whoever recorded it typed. Free text.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The report: an attachment. No foreign key, as attachments have none.</summary>
    public Guid ReportAttachmentId { get; init; }

    public DateTime ReportedAtUtc { get; init; }

    /// <summary>Who attached the report (the lab) or recorded the external result.</summary>
    public Guid ReportedByUserId { get; init; }

    /// <summary>The clinician who must sign it off: the specimen's requesting clinician, or whoever it's given to.</summary>
    public Guid ResponsibleUserId { get; set; }

    public ResultStatus Status { get; set; }

    public DateTime? SeenAtUtc { get; set; }
    public Guid? SeenByUserId { get; set; }

    public DateTime? SignedOffAtUtc { get; set; }
    public Guid? SignedOffByUserId { get; set; }

    /// <summary>What the clinician who signed it off did.</summary>
    public ResultAction? Action { get; set; }

    /// <summary>Their detail (required for Other). Free text.</summary>
    public string? ActionDetail { get; set; }

    public DateTime? EnteredInErrorAtUtc { get; set; }
    public Guid? EnteredInErrorByUserId { get; set; }

    /// <summary>What was wrong, when it was marked entered in error. Free text.</summary>
    public string? ErrorDetail { get; set; }

    /// <summary>One more for each change. A change names the version it started from (409 if it's changed since).</summary>
    public int Version { get; set; }

    /// <summary>The database's row version, a concurrency token: two saves at once can't both win.</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Still to be signed off (awaiting, or seen).</summary>
    public bool IsAwaitingSignOff => Status is ResultStatus.AwaitingSignOff or ResultStatus.Seen;
}
