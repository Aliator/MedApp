namespace MedApp.Contracts.Referrals.Responses;

/// <summary>One change to a referral, from its history: structured values only, never free text.</summary>
public sealed class ReferralEventResponse
{
    public Guid Id { get; set; }

    /// <summary>Registered, DetailsEdited, Withdrawn or EnteredInError (triage and the clock add more).</summary>
    public string Action { get; set; } = string.Empty;

    public DateTime AtUtc { get; set; }

    /// <summary>Who did it (the username at the time).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The status after it, when it changed one.</summary>
    public string? ToStatus { get; set; }

    /// <summary>Registered: the specialty asked for. DetailsEdited: the specialty after, if it changed.</summary>
    public string? SpecialtyName { get; set; }

    /// <summary>DetailsEdited: the specialty before, if it changed.</summary>
    public string? PreviousSpecialtyName { get; set; }

    /// <summary>Registered: the priority asked for.</summary>
    public string? RequestedPriority { get; set; }

    /// <summary>DetailsEdited: the names of the fields that changed (e.g. reason, receivedOn), never their old text.</summary>
    public List<string> ChangedFields { get; set; } = [];
}
