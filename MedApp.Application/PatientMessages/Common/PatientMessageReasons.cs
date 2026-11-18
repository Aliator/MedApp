namespace MedApp.Application.PatientMessages.Common;

/// <summary>
/// The fixed codes kept as a message's <c>StatusReason</c> (never free text). A provider's own delivery status
/// (e.g. <c>permanent-failure</c>) is kept as it comes.
/// </summary>
public static class PatientMessageReasons
{
    /// <summary>The patient hasn't agreed to reminders (their reminder choice is None).</summary>
    public const string NoConsent = "NoConsent";

    /// <summary>They chose texts, and have no mobile.</summary>
    public const string NoMobile = "NoMobile";

    /// <summary>They chose email, and have no address.</summary>
    public const string NoEmail = "NoEmail";

    /// <summary>The appointment changed (moved, cancelled, outcome corrected) or passed before it was sent.</summary>
    public const string NoLongerApplies = "NoLongerApplies";

    /// <summary>The patient's reminder choice no longer includes this channel, or they no longer have the contact.</summary>
    public const string ChoiceChanged = "ChoiceChanged";

    /// <summary>The record was restricted or anonymised before it was sent.</summary>
    public const string RecordClosed = "RecordClosed";

    /// <summary>The provider refused it (an invalid number or address, say).</summary>
    public const string Rejected = "Rejected";

    /// <summary>
    /// The provider didn't answer in time: it may have been sent or not. Not tried again, so it can't go twice.
    /// </summary>
    public const string Unconfirmed = "Unconfirmed";

    /// <summary>Sending kept failing for a reason that might have passed, and was given up.</summary>
    public const string TooManyAttempts = "TooManyAttempts";
}
