using MedApp.Contracts.Pathways.PatientPathways.Responses;

namespace MedApp.Contracts.Mdt.Responses;

/// <summary>A team's MDT (a meeting marked as one), with how many items are on it.</summary>
public sealed class MdtMeetingResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;

    /// <summary>Items listed or deferred to it, still to be discussed.</summary>
    public int OpenItemCount { get; set; }

    /// <summary>Every item on it, in any status.</summary>
    public int ItemCount { get; set; }

    /// <summary>Usernames of those invited, for choosing who was present.</summary>
    public List<MdtPersonResponse> Attendees { get; set; } = [];
}

/// <summary>A member of staff.</summary>
public sealed class MdtPersonResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}

/// <summary>One meeting's list, in the order items were listed.</summary>
public sealed class MdtMeetingListResponse
{
    public MdtMeetingResponse Meeting { get; set; } = new();

    /// <summary>
    /// Why the viewer sees only part of the list, or none of it (MC26): another team's MDT they attend (the cases listed,
    /// read only), or one they aren't invited to; null for the MDT's own team and admins.
    /// </summary>
    public string? ReadOnlyReason { get; set; }

    public List<MdtItemResponse> Items { get; set; } = [];
}

/// <summary>
/// A pathway put forward for an MDT: the patient (as the banner shows them), the pathway, the meeting, the question
/// and, once discussed, the outcome as a person wrote it and who was present.
/// </summary>
public sealed class MdtItemResponse
{
    public Guid Id { get; set; }

    /// <summary>Listed, Discussed, Deferred or Withdrawn.</summary>
    public string Status { get; set; } = string.Empty;

    public PathwayPatientResponse? Patient { get; set; }

    public Guid PathwayId { get; set; }
    public string PathwayTitle { get; set; } = string.Empty;

    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;

    /// <summary>The meeting it's on; null once that meeting was cancelled.</summary>
    public Guid? MeetingId { get; set; }
    public string? MeetingTitle { get; set; }
    public DateTime? MeetingStartUtc { get; set; }

    /// <summary>When its cancelled meeting would have started: an open item then needs listing again.</summary>
    public DateTime? MeetingCancelledStartUtc { get; set; }

    public string Question { get; set; } = string.Empty;

    public string ListedByUsername { get; set; } = string.Empty;
    public DateTime ListedAtUtc { get; set; }

    public string? Outcome { get; set; }
    public string? OutcomeByUsername { get; set; }
    public DateTime? OutcomeAtUtc { get; set; }

    /// <summary>Who was present when it was discussed.</summary>
    public List<string> Present { get; set; } = [];

    public string? WithdrawnByUsername { get; set; }
    public DateTime? WithdrawnAtUtc { get; set; }

    public int Version { get; set; }

    /// <summary>Whether the viewer can take it off the list now.</summary>
    public bool CanWithdraw { get; set; }

    /// <summary>Whether the viewer can move it to another meeting now.</summary>
    public bool CanDefer { get; set; }

    /// <summary>Whether the viewer can record its outcome now (its meeting has started).</summary>
    public bool CanRecordOutcome { get; set; }

    /// <summary>
    /// Why it can't change now (the record is restricted or anonymised, or it's another team's item the viewer reads as
    /// an attendee of its MDT), or null.
    /// </summary>
    public string? ReadOnlyReason { get; set; }

    /// <summary>Who presents it (MC21), from its meeting's attendees; null for nobody.</summary>
    public Guid? PresenterUserId { get; set; }
    public string? PresenterUsername { get; set; }

    /// <summary>Whether the viewer can choose its presenter now (its team's, open, on a meeting).</summary>
    public bool CanChoosePresenter { get; set; }

    /// <summary>The lab's work on its pathway (MC21): the samples, oldest taken first, with their reports' results.</summary>
    public List<MdtLabWorkResponse> LabWork { get; set; } = [];
}

/// <summary>A sample on an MDT item's pathway, with its result once reported (MC21).</summary>
public sealed class MdtLabWorkResponse
{
    public Guid SpecimenId { get; set; }

    public string Number { get; set; } = string.Empty;

    public string? TypeName { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? ReportedAtUtc { get; set; }

    /// <summary>The result its report made, for the sign-off page; null until reported.</summary>
    public Guid? ResultId { get; set; }
}
