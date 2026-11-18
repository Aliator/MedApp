using MedApp.Application.Activity.Common;

namespace MedApp.Application.Jobs.Common;

/// <summary>
/// The activity log's actor for scheduled jobs: the system ("medapp-jobs"), with no user and no address, wherever a job
/// records who did something.
/// </summary>
public sealed class SystemActivityContext : IActivityContext
{
    public const string Username = "medapp-jobs";

    public static readonly SystemActivityContext Instance = new();

    public ActivityActor Actor { get; } = new(null, Username, null);
}
