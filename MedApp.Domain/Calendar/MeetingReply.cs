namespace MedApp.Domain.Calendar;

/// <summary>
/// An attendee's answer to a meeting invitation. Stored as its number, so never renumber these. Moving a meeting to
/// another time puts everyone back to <see cref="NoReply"/>.
/// </summary>
public enum MeetingReply
{
    /// <summary>Not answered yet (or the meeting moved since): it counts as busy time, as an accepted one does.</summary>
    NoReply = 0,

    /// <summary>Going.</summary>
    Accepted = 1,

    /// <summary>Not going: the meeting is no longer busy time on their calendar.</summary>
    Declined = 2
}
