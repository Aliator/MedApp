using MedApp.Application.Activity.Common;

namespace MedApp.DevMcp;

/// <summary>The activity log's actor for changes this tool makes.</summary>
public sealed class DevToolActivityContext : IActivityContext
{
    public const string Username = "medapp-dev";

    public ActivityActor Actor { get; } = new(null, Username, null);
}
