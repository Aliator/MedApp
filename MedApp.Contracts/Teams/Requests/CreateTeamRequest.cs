namespace MedApp.Contracts.Teams.Requests;

public sealed class CreateTeamRequest
{
    /// <summary>Unique, ignoring case; at most 100 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The waiting-time target, in days (1 to 365), for Urgent referrals. Left out: 14 for a new team, unchanged on an edit.</summary>
    public int? TargetDaysUrgent { get; set; }

    /// <summary>For High (default 28).</summary>
    public int? TargetDaysHigh { get; set; }

    /// <summary>For Medium (default 126).</summary>
    public int? TargetDaysMedium { get; set; }

    /// <summary>For Low (default 126).</summary>
    public int? TargetDaysLow { get; set; }
}
