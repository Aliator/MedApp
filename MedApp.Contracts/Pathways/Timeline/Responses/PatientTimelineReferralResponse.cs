namespace MedApp.Contracts.Pathways.Timeline.Responses;

/// <summary>A referral awaiting triage, beside the open pathways above a patient's timeline.</summary>
public sealed class PatientTimelineReferralResponse
{
    public Guid ReferralId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public DateOnly ReceivedOn { get; set; }

    /// <summary>Low, Medium, High or Urgent.</summary>
    public string RequestedPriority { get; set; } = string.Empty;
}
