namespace MedApp.Domain.Referrals;

/// <summary>
/// One change to a referral: who did what, and when. Written in the same save as the change. Like the pathway audit
/// log and appointments' history, it has no foreign keys, so it outlives the accounts and pathway it names. Its
/// <see cref="Details"/> hold structured values only (statuses, enums, specialty IDs and names, dates, the names of the
/// fields changed), never free text: the reason, the referrer and the details given live only on the referral.
/// </summary>
public sealed class ReferralEvent
{
    public Guid Id { get; init; }

    public Guid ReferralId { get; init; }

    public Guid PatientId { get; init; }

    /// <summary>The pathway the referral was accepted into, if it had one at the time.</summary>
    public Guid? PathwayId { get; init; }

    public ReferralEventAction Action { get; init; }

    public DateTime AtUtc { get; init; }

    /// <summary>Who made the change.</summary>
    public Guid? UserId { get; init; }

    /// <summary>The username at the time, kept even if the account is later deleted.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>What changed, as camelCase JSON, with no free text.</summary>
    public string Details { get; set; } = "{}";
}
