namespace MedApp.Contracts.Procedures.Insights.Responses;

/// <summary>
/// An overview of procedure work for admins: how many are open, in progress, overdue and completed (each against the
/// same point last week), where the overdue work is, how long each stage takes, and how many were created and
/// completed each week. Weeks are UK weeks, Monday to Sunday.
/// </summary>
public sealed class ProcedureInsightsResponse
{
    /// <summary>How many weeks it covers, ending with this one.</summary>
    public int Weeks { get; set; }

    /// <summary>The template it's limited to, if any.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>The Monday the first week starts on (UK).</summary>
    public DateOnly From { get; set; }

    /// <summary>Today (UK): the current week runs to now.</summary>
    public DateOnly To { get; set; }

    /// <summary>Not completed, cancelled or archived (blocked ones included).</summary>
    public InsightsComparisonResponse Open { get; set; } = new();

    public InsightsComparisonResponse InProgress { get; set; } = new();

    /// <summary>Open (not blocked) and past their due date.</summary>
    public InsightsComparisonResponse Overdue { get; set; } = new();

    /// <summary>Marked completed this week so far, against last week up to the same point.</summary>
    public InsightsComparisonResponse CompletedThisWeek { get; set; } = new();

    /// <summary>Overdue procedures by who they're assigned to, most first; a procedure with two assignees counts for each.</summary>
    public List<OverdueByAssigneeResponse> OverdueByAssignee { get; set; } = [];

    /// <summary>Overdue procedures by priority, most urgent first, every priority listed.</summary>
    public List<OverdueByPriorityResponse> OverdueByPriority { get; set; } = [];

    /// <summary>The average time at each stage, per template, over the stages finished in the period.</summary>
    public List<TemplateStageTimesResponse> StageTimes { get; set; } = [];

    /// <summary>Procedures created and completed each week, oldest first; the last is this week so far.</summary>
    public List<WeeklyThroughputResponse> Throughput { get; set; } = [];
}

/// <summary>A count now, and at the same point last week.</summary>
public sealed class InsightsComparisonResponse
{
    public int Current { get; set; }

    public int LastWeek { get; set; }
}

public sealed class OverdueByAssigneeResponse
{
    /// <summary>Null for the procedures nobody is assigned.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Null when nobody is assigned, or the account has been deleted.</summary>
    public string? Username { get; set; }

    public int Count { get; set; }
}

public sealed class OverdueByPriorityResponse
{
    public string Priority { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>One template's stages, in order, with how long each took on average.</summary>
public sealed class TemplateStageTimesResponse
{
    /// <summary>Null for procedures made before templates were required.</summary>
    public Guid? TemplateId { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    public List<StageTimeResponse> Stages { get; set; } = [];
}

/// <summary>
/// A stage's average times: waiting is from reaching it (the stage before finished, or the procedure created) to
/// starting it; working is from starting it to finishing it.
/// </summary>
public sealed class StageTimeResponse
{
    public int StageOrder { get; set; }

    public string StageName { get; set; } = string.Empty;

    /// <summary>How many procedures finished this stage in the period: what the averages are over.</summary>
    public int FinishedCount { get; set; }

    public double AverageWaitingHours { get; set; }

    public double AverageWorkingHours { get; set; }
}

public sealed class WeeklyThroughputResponse
{
    /// <summary>The week's Monday (UK).</summary>
    public DateOnly WeekStart { get; set; }

    public int Created { get; set; }

    /// <summary>Times procedures were marked completed.</summary>
    public int Completed { get; set; }
}
