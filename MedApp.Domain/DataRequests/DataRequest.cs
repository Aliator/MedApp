using MedApp.Domain.Patients;

namespace MedApp.Domain.DataRequests;

/// <summary>
/// A patient's subject access request (UK GDPR Article 15), logged so it's answered in time: who asked, the identity
/// check, the due date (one calendar month from receipt, extendable by up to two more), the clinician review that
/// withholds what meets the serious harm test, and how it ended (<c>docs/centre-phases-3-5-plan.md</c>, decisions 160 to
/// 163). The response pack is built on request and downloaded, never stored.
/// <para>
/// Patient data: exported with the record; the requester's name and relationship, the identity check's detail, the
/// extension's and refusal's reasons and the withheld items' reasons are free text, replaced on anonymisation. Changes
/// are recorded in the activity log (<c>DataRequestChanged</c>), with field names only.
/// </para>
/// </summary>
public sealed class DataRequest
{
    public const int MaxRequesterNameLength = 200;
    public const int MaxRelationshipLength = 100;
    public const int MaxDetailLength = 500;

    /// <summary>The most months the due date can be extended by.</summary>
    public const int MaxExtensionMonths = 2;

    public Guid Id { get; init; }

    public Guid PatientId { get; init; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The UK date the organisation received it: the one-month clock starts here.</summary>
    public DateOnly ReceivedOn { get; init; }

    /// <summary>Whether the patient asked themselves; otherwise someone on their behalf (<see cref="RequesterName"/>).</summary>
    public bool RequestedByPatient { get; init; }

    /// <summary>Who asked on the patient's behalf (free text), or null when the patient asked.</summary>
    public string? RequesterName { get; set; }

    /// <summary>Their relationship to the patient, or their authority (free text), or null when the patient asked.</summary>
    public string? RequesterRelationship { get; set; }

    public DateOnly? IdentityCheckedOn { get; set; }
    public Guid? IdentityCheckedByUserId { get; set; }
    public string? IdentityCheckedByUsername { get; set; }
    public IdentityCheckMethod? IdentityCheckMethod { get; set; }

    /// <summary>How it was checked, in words (required for <see cref="DataRequests.IdentityCheckMethod.Other"/>).</summary>
    public string? IdentityCheckDetail { get; set; }

    public bool IsIdentityChecked => IdentityCheckedOn is not null;

    /// <summary>When the response is due: <see cref="ReceivedOn"/> plus one month, plus <see cref="ExtensionMonths"/>.</summary>
    public DateOnly DueOn { get; set; }

    /// <summary>0, 1 or 2 more months.</summary>
    public int ExtensionMonths { get; set; }

    public string? ExtensionReason { get; set; }

    public DataRequestStatus Status { get; set; }

    /// <summary>Why it was refused (free text), for <see cref="DataRequestStatus.Refused"/>.</summary>
    public string? RefusalReason { get; set; }

    /// <summary>The UK date the response was sent, for <see cref="DataRequestStatus.Sent"/>.</summary>
    public DateOnly? SentOn { get; set; }

    public DataRequestSendMethod? SendMethod { get; set; }

    /// <summary>When and by whom it was sent, refused or withdrawn.</summary>
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosedByUsername { get; set; }

    /// <summary>When a clinician last recorded the review, and who: the pack can't be made before.</summary>
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByUsername { get; set; }

    public bool IsReviewed => ReviewedAtUtc is not null;

    /// <summary>What the review withheld, each with its reason; replaced as a whole by each review.</summary>
    public List<DataRequestWithheldItem> WithheldItems { get; set; } = [];

    /// <summary>
    /// The due date the "due soon" notification was sent for, so the daily job tells people once per due date (an
    /// extension, moving it, tells them again).
    /// </summary>
    public DateOnly? DueSoonNotifiedFor { get; set; }

    public DateTime CreatedAtUtc { get; init; }
    public Guid CreatedByUserId { get; init; }
    public string CreatedByUsername { get; init; } = string.Empty;
    public DateTime LastUpdatedUtc { get; set; }

    /// <summary>Bumped by every change; a change carries the version the caller saw (<c>expectedVersion</c>).</summary>
    public int Version { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public bool IsOpen => Status == DataRequestStatus.Open;
}

/// <summary>An item of the record the clinician review withheld from the pack under the serious harm test, with why.</summary>
public sealed class DataRequestWithheldItem
{
    public Guid Id { get; init; }
    public Guid DataRequestId { get; init; }
    public DataRequestItemKind Kind { get; init; }

    /// <summary>The note's, file's, letter's, result's, MDT item's or referral's ID (no foreign key).</summary>
    public Guid ItemId { get; init; }

    /// <summary>Why it's withheld (free text, the clinician's).</summary>
    public string Reason { get; set; } = string.Empty;
}
