using MedApp.Domain.Teams;

namespace MedApp.Application.Teams.Common;

public static class TeamLimits
{
    public const int MaxNameLength = Team.MaxNameLength;

    /// <summary>The most teams one member of staff can be given at once (far more than any centre has).</summary>
    public const int MaxPerUser = 100;
}
