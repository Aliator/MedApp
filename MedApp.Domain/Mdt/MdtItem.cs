using MedApp.Domain.Calendar;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Teams;

namespace MedApp.Domain.Mdt;

/// <summary>
/// A pathway put forward for a team's multidisciplinary team meeting (an MDT: a calendar <see cref="Meeting"/>
/// marked <see cref="Meeting.IsMdt"/>), with the question for the team, and once discussed the outcome as a person wrote
/// it and who was present. MedApp never suggests an outcome or acts on one: any change to the pathway that follows is a
/// person's own (<c>docs/centre-phases-3-5-plan.md</c>, decisions 141 to 144).
/// <para>
/// Patient data: the question and the outcome are free text, shown on the MDT list and the pathway's MDT card, never in
/// the audit log, a notification or a staff timeline. Its history is in the pathway audit log
/// (<c>MdtListed</c>, <c>MdtDeferred</c>, <c>MdtWithdrawn</c>, <c>MdtOutcomeRecorded</c>).
/// </para>
/// </summary>
public sealed class MdtItem
{
    public const int MaxQuestionLength = 1000;
    public const int MaxOutcomeLength = 2000;

    public Guid Id { get; init; }

    public Guid PatientId { get; init; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The pathway it's about. Deleting the pathway deletes its items (their history stays in the audit log).</summary>
    public Guid PathwayId { get; init; }
    public PatientPathway Pathway { get; set; } = null!;

    /// <summary>The team whose MDT it's for: its meeting's.</summary>
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    /// <summary>The meeting it's on; null once that meeting was cancelled (see <see cref="MeetingCancelledStartUtc"/>).</summary>
    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    /// <summary>
    /// When the meeting it was on would have started, if that meeting was cancelled: an open item is then "Meeting
    /// cancelled: list again". Cleared when it's put on another meeting.
    /// </summary>
    public DateTime? MeetingCancelledStartUtc { get; set; }

    /// <summary>The question for discussion (free text).</summary>
    public string Question { get; set; } = string.Empty;

    public MdtItemStatus Status { get; set; }

    public Guid ListedByUserId { get; init; }

    /// <summary>The username at the time, kept if the account is deleted.</summary>
    public string ListedByUsername { get; init; } = string.Empty;

    public DateTime ListedAtUtc { get; init; }

    /// <summary>What the team decided, as the person who recorded it wrote it (free text); null until discussed.</summary>
    public string? Outcome { get; set; }

    public Guid? OutcomeByUserId { get; set; }
    public string? OutcomeByUsername { get; set; }
    public DateTime? OutcomeAtUtc { get; set; }

    public Guid? WithdrawnByUserId { get; set; }
    public string? WithdrawnByUsername { get; set; }
    public DateTime? WithdrawnAtUtc { get; set; }

    /// <summary>
    /// Who presents it at the MDT (MC21; a pathologist, say), chosen by the item's team from its meeting's attendees;
    /// null for nobody. Kept on a move only if they're invited to the new MDT too.
    /// </summary>
    public Guid? PresenterUserId { get; set; }

    /// <summary>The presenter's username at the time, kept if the account is deleted.</summary>
    public string? PresenterUsername { get; set; }

    /// <summary>Who was present when it was discussed, chosen from the meeting's attendees.</summary>
    public List<MdtItemAttendee> Attendees { get; set; } = [];

    /// <summary>1 when listed, one more with each change; a change names the version it started from.</summary>
    public int Version { get; set; }

    /// <summary>Changes on every save, so the later of two overlapping changes is refused (a 409).</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Listed or deferred: still to be discussed.</summary>
    public bool IsOpen => Status is MdtItemStatus.Listed or MdtItemStatus.Deferred;
}
