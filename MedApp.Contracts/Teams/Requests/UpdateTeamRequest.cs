namespace MedApp.Contracts.Teams.Requests;

/// <summary>Renames a team. What has it (templates, pathways, staff) shows the new name.</summary>
public sealed class UpdateTeamRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The <c>version</c> the caller saw: if anyone has changed the team since, it's a 409.</summary>
    public int ExpectedVersion { get; set; }

    /// <summary>The waiting-time target, in days (1 to 365), for Urgent referrals. Left out: 14 for a new team, unchanged on an edit.</summary>
    public int? TargetDaysUrgent { get; set; }

    /// <summary>For High (default 28).</summary>
    public int? TargetDaysHigh { get; set; }

    /// <summary>For Medium (default 126).</summary>
    public int? TargetDaysMedium { get; set; }

    /// <summary>For Low (default 126).</summary>
    public int? TargetDaysLow { get; set; }
}
