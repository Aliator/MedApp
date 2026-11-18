namespace MedApp.Contracts.Mdt.Requests;

/// <summary>Lists a pathway for an MDT: the meeting, and the question for the team.</summary>
public sealed class ListForMdtRequest
{
    public Guid PathwayId { get; set; }
    public Guid MeetingId { get; set; }
    public string Question { get; set; } = string.Empty;
}

/// <summary>Takes an item off the list, from the version the caller saw.</summary>
public sealed class WithdrawMdtItemRequest
{
    public int ExpectedVersion { get; set; }
}

/// <summary>Moves an item to a later MDT (or lists it again after its meeting was cancelled).</summary>
public sealed class DeferMdtItemRequest
{
    public Guid MeetingId { get; set; }
    public int ExpectedVersion { get; set; }
}

/// <summary>The team's outcome, as written by the person recording it, and who was present (from the meeting's attendees).</summary>
public sealed class RecordMdtOutcomeRequest
{
    public string Outcome { get; set; } = string.Empty;
    public List<Guid> PresentUserIds { get; set; } = [];
    public int ExpectedVersion { get; set; }
}
