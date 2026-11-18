namespace MedApp.Application.Jobs.Common;

/// <summary>
/// The <c>Jobs</c> settings: whether this copy of the API runs jobs on their schedules (<c>Jobs:Enabled</c>, default
/// on; the integration tests turn it off and run jobs themselves), and each job's own switch
/// (<c>Jobs:{Name}:Enabled</c>, default on), which also stops Run now.
/// </summary>
public sealed class JobsOptions
{
    public const string Section = "Jobs";

    /// <summary>Whether this copy runs jobs on their schedules.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Each job switched on or off by its name; a job not named here is on.</summary>
    public Dictionary<string, bool> JobEnabled { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsJobEnabled(string name) => !JobEnabled.TryGetValue(name, out var enabled) || enabled;
}
