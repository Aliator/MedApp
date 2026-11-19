namespace MedApp.Domain.PatientMessages;

/// <summary>
/// One text or email to a patient about an appointment (centre plan decisions 128 to 133): what was (or would have
/// been) sent, to where, and what became of it. Every message is stored, those never sendable too
/// (<see cref="PatientMessageStatus.NotSent"/>), so the appointment shows whether the patient was told. A message is
/// unique per appointment, kind, channel, appointment start and room, so a job cut short, or a reschedule, never sends one
/// twice, and a move to another room (perhaps another site) is a new message. No foreign keys, like the appointment's history: it outlives what it's about.
/// <para>
/// It's patient data: exported, anonymised (destination and text replaced) and shown on the appointment. Its text holds
/// no clinical words: the organisation, the date and time, the site's name, address and phone (<c>PatientMessageTexts</c>).
/// </para>
/// </summary>
public sealed class PatientMessage
{
    /// <summary>The longest a destination can be: an email address.</summary>
    public const int MaxDestinationLength = 320;

    /// <summary>The longest a message's text can be.</summary>
    public const int MaxBodyLength = 1000;

    /// <summary>The longest a status reason or provider reference can be.</summary>
    public const int MaxCodeLength = 100;

    public Guid Id { get; init; }

    public Guid PatientId { get; init; }

    /// <summary>
    /// The team it belongs to (the team walls, decision 200 of <c>docs/multi-clinic-plan.md</c>): its appointment's. Set when
    /// it's made, never edited by hand.
    /// </summary>
    public Guid TeamId { get; set; }

    public Guid AppointmentId { get; init; }

    public PatientMessageKind Kind { get; init; }

    public PatientMessageChannel Channel { get; init; }

    /// <summary>The mobile (<c>+447…</c>) or email address it went to, as sent; empty when there was none.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>The text, as sent (for an email, its body; the subject is fixed for each kind).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>The appointment's start the message is about (part of its unique key).</summary>
    public DateTime AppointmentStartUtc { get; init; }

    /// <summary>
    /// The appointment's room the message is about, whose site its text names (part of its unique key); null for an
    /// appointment with no room. A message for another room than the appointment's now no longer applies.
    /// </summary>
    public Guid? AppointmentRoomId { get; init; }

    public PatientMessageStatus Status { get; set; }

    /// <summary>
    /// Why it's <see cref="PatientMessageStatus.NotSent"/>, <see cref="PatientMessageStatus.Cancelled"/> or
    /// <see cref="PatientMessageStatus.Failed"/>, as a fixed code (<c>PatientMessageReasons</c>, or the provider's status
    /// name), never free text.
    /// </summary>
    public string? StatusReason { get; set; }

    /// <summary>The provider's ID for it, to ask about delivery.</summary>
    public string? ProviderReference { get; set; }

    /// <summary>Whoever asked for it (staff moving or cancelling the appointment); null for the job's own.</summary>
    public Guid? RequestedByUserId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>When it was handed to the provider.</summary>
    public DateTime? SentAtUtc { get; set; }

    /// <summary>When its status last changed, or was last asked about.</summary>
    public DateTime? StatusAtUtc { get; set; }

    /// <summary>How many times sending it failed for a reason that may pass (the provider away), before it's given up.</summary>
    public int SendAttempts { get; set; }

    /// <summary>
    /// The database's row version, a concurrency token: the job's save after sending can't overwrite an anonymisation
    /// (or another change) saved meanwhile.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
