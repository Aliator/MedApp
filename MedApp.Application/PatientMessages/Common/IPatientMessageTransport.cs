using MedApp.Domain.PatientMessages;

namespace MedApp.Application.PatientMessages.Common;

/// <summary>
/// Hands patient messages to whatever delivers them (centre plan decision 128), chosen by
/// <see cref="PatientMessageOptions.Mode"/>: none (Off), the offline stand-in that emails Mailpit, or GOV.UK Notify.
/// Only the <c>patient-reminders</c> job calls it, never a request. It never logs a destination or a message's text.
/// </summary>
public interface IPatientMessageTransport
{
    PatientMessageMode Mode { get; }

    /// <summary>
    /// Sends one message (its destination, channel and text as stored; an email's subject from its kind). A failure that
    /// may pass (the provider away, a timeout) throws; one that won't is <see cref="PatientMessageSendResult.Rejected"/>.
    /// </summary>
    Task<PatientMessageSendResult> SendAsync(PatientMessage message, CancellationToken ct);

    /// <summary>What the provider says became of a message it was given (by its reference).</summary>
    Task<PatientMessageDelivery> GetDeliveryAsync(string providerReference, CancellationToken ct);
}

/// <summary>What became of handing a message over.</summary>
public sealed record PatientMessageSendResult
{
    private PatientMessageSendResult(PatientMessageSendOutcome outcome, string? reference, string? reason)
    {
        Outcome = outcome;
        Reference = reference;
        Reason = reason;
    }

    public PatientMessageSendOutcome Outcome { get; }

    /// <summary>The provider's ID for it, when accepted.</summary>
    public string? Reference { get; }

    /// <summary>Why it was refused, as a fixed code, when rejected.</summary>
    public string? Reason { get; }

    /// <summary>The provider took it.</summary>
    public static PatientMessageSendResult Accepted(string reference) => new(PatientMessageSendOutcome.Accepted, reference, null);

    /// <summary>The provider refused it, and would again (an invalid number, say).</summary>
    public static PatientMessageSendResult Rejected(string reason) => new(PatientMessageSendOutcome.Rejected, null, reason);

    /// <summary>The provider asked to slow down: try this and the rest on the next run, without counting an attempt.</summary>
    public static PatientMessageSendResult TryLater() => new(PatientMessageSendOutcome.TryLater, null, null);
}

public enum PatientMessageSendOutcome
{
    Accepted,
    Rejected,
    TryLater
}

/// <summary>A provider's answer about delivery: still on its way, delivered, or failed (with its status name).</summary>
public sealed record PatientMessageDelivery(PatientMessageStatus Status, string? Reason = null)
{
    public static readonly PatientMessageDelivery OnItsWay = new(PatientMessageStatus.Sent);

    public static readonly PatientMessageDelivery Delivered = new(PatientMessageStatus.Delivered);

    public static PatientMessageDelivery Failed(string reason) => new(PatientMessageStatus.Failed, reason);
}
