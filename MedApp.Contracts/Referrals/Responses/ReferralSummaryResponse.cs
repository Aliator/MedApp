namespace MedApp.Contracts.Referrals.Responses;

/// <summary>A referral in a list: who, what for, from whom and when, without its free-text reason.</summary>
public sealed class ReferralSummaryResponse
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>The patient's full name.</summary>
    public string PatientName { get; set; } = string.Empty;

    /// <summary>The NHS number, as its ten digits, or null when it isn't known.</summary>
    public string? PatientNhsNumber { get; set; }

    public DateOnly PatientDateOfBirth { get; set; }

    public Guid SpecialtyId { get; set; }

    public string SpecialtyName { get; set; } = string.Empty;

    public DateOnly ReceivedOn { get; set; }

    /// <summary>Whole days since it was received, up to today (UK) while it's open, or to when it was decided.</summary>
    public int DaysSinceReceived { get; set; }

    /// <summary>Low, Medium, High or Urgent.</summary>
    public string RequestedPriority { get; set; } = string.Empty;

    public string ReferrerPractice { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>Whether a file (its letter) is attached.</summary>
    public bool HasLetter { get; set; }

    public DateTime RegisteredAtUtc { get; set; }

    public DateTime? DecidedAtUtc { get; set; }
}
