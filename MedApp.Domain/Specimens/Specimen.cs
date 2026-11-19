using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;

namespace MedApp.Domain.Specimens;

/// <summary>
/// Something taken from a patient at a stage of a pathway and sent to a lab, tracked there and back by its own number
/// (<see cref="Number"/>, from a SQL sequence: <c>MA26-000123</c>) until the lab's report is attached. Its history is
/// in the pathway audit log (<see cref="PatientPathwayAuditAction.SpecimenTaken"/> and on), whose details never hold
/// its free text. MedApp tracks and shows it; it never reads or interprets the report.
/// <para>
/// Patient data: the site and request notes are free text, shown on its page and request sheet, never on a staff
/// timeline, in a notification or a log.
/// </para>
/// </summary>
public sealed class Specimen
{
    public const int MaxNumberLength = 16;
    public const int MaxSiteLength = 200;
    public const int MaxRequestNotesLength = 1000;
    public const int MaxReferenceLength = 50;
    public const int MaxClosedDetailLength = 500;

    public Guid Id { get; init; }

    /// <summary>MedApp's own specimen number, <c>{prefix}{yy}-{nnnnnn}</c>, unique; printed on the label (and its QR code).</summary>
    public string Number { get; init; } = string.Empty;

    public Guid PatientId { get; init; }

    /// <summary>
    /// The team it belongs to (the team walls, decision 200 of <c>docs/multi-clinic-plan.md</c>): its pathway's. Set when
    /// it's made, never edited by hand.
    /// </summary>
    public Guid TeamId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Where it came from (MC14): a clinic's pathway, or the hospital (MC18). Stored by number.</summary>
    public SpecimenSource Source { get; init; }

    /// <summary>The clinic that asked for it (a clinic sample's pathway team); null for the hospital's.</summary>
    public Guid? RequestingTeamId { get; init; }
    public MedApp.Domain.Teams.Team? RequestingTeam { get; set; }

    /// <summary>The hospital that sent it and the request it came with (MC18); null for a clinic's.</summary>
    public Guid? HospitalId { get; init; }
    public MedApp.Domain.Hospitals.Hospital? Hospital { get; set; }

    public Guid? HospitalRequestId { get; init; }
    public MedApp.Domain.Hospitals.HospitalRequest? HospitalRequest { get; set; }

    /// <summary>The test asked for, from the lab's catalogue (MC13), if one was chosen (never chosen for the clinician).</summary>
    public Guid? LabTestId { get; set; }
    public MedApp.Domain.Lab.LabTest? LabTest { get; set; }

    /// <summary>The lab case it's in (MC15), once the lab has it; null until then, or after its case was entered in error.</summary>
    public Guid? LabCaseId { get; set; }
    public MedApp.Domain.Lab.LabCase? LabCase { get; set; }

    /// <summary>The pathway whose stage took it; null for a hospital sample.</summary>
    public Guid? PathwayId { get; init; }
    public PatientPathway? Pathway { get; set; }

    /// <summary>
    /// The stage that took it (null for a hospital sample). No foreign key: a pathway's stages go only with the pathway,
    /// which can't be deleted while it has specimens.
    /// </summary>
    public Guid? StageId { get; init; }

    public Guid SpecimenTypeId { get; set; }
    public SpecimenType SpecimenType { get; set; } = null!;


    /// <summary>Where on the patient it was taken from ("left forearm"), if given. Free text.</summary>
    public string? Site { get; set; }

    /// <summary>Notes for the lab, if any. Free text, clinical.</summary>
    public string? RequestNotes { get; set; }

    /// <summary>The clinician who asked for it, and who will get its result.</summary>
    public Guid RequestingUserId { get; set; }

    public SpecimenStatus Status { get; set; }

    public DateTime TakenAtUtc { get; set; }
    public Guid TakenByUserId { get; init; }

    /// <summary>
    /// When it was recorded as taken (the taken time may be typed up to 7 days back): the 24 hours in which whoever
    /// recorded it may mark it entered in error run from here.
    /// </summary>
    public DateTime RecordedAtUtc { get; init; }

    public DateTime? SentAtUtc { get; set; }
    public Guid? SentByUserId { get; set; }

    /// <summary>The courier's or batch's reference, if given.</summary>
    public string? CourierReference { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }
    public Guid? ReceivedByUserId { get; set; }

    /// <summary>The lab's own reference for it, if given.</summary>
    public string? LabReference { get; set; }

    public DateTime? ReportedAtUtc { get; set; }
    public Guid? ReportedByUserId { get; set; }

    /// <summary>
    /// The report: an attachment on the pathway (or the patient, if the pathway had closed). No foreign key, as
    /// attachments have none.
    /// </summary>
    public Guid? ReportAttachmentId { get; set; }

    /// <summary>Rejected by the lab: why, as a fixed choice.</summary>
    public SpecimenRejectionReason? RejectionReason { get; set; }

    /// <summary>The detail given when it was rejected, lost or entered in error. Free text.</summary>
    public string? ClosedDetail { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }

    /// <summary>One more for each change. A change names the version it started from (409 if it's changed since).</summary>
    public int Version { get; set; }

    /// <summary>The database's row version, a concurrency token: two saves at once can't both win.</summary>
    public byte[] RowVersion { get; set; } = [];
}
