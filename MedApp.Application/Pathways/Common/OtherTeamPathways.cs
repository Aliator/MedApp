using MedApp.Application.Common.Teams;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// A patient's pathways the viewer can't see, as summary lines only (decision 183, hazard H-66): the team, in progress or
/// completed, and since when. Cancelled and archived ones aren't summarised (deleted ones are gone). Admins see every
/// team's in full, so they get none. Newest first, then by team name.
/// </summary>
public static class OtherTeamPathways
{
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    private static readonly IReadOnlySet<PatientPathwayStatus> NotSummarised = new HashSet<PatientPathwayStatus>
    {
        PatientPathwayStatus.Cancelled,
        PatientPathwayStatus.Archived,
    };

    /// <param name="pathways">Every pathway of the patient's (every team's).</param>
    /// <param name="visibleIds">Those the viewer sees in full.</param>
    /// <param name="teamNames">The teams' names, by ID.</param>
    /// <param name="scope">The viewer's team walls.</param>
    public static List<OtherTeamPathwaySummaryResponse> Summarise(
        IEnumerable<PatientPathway> pathways,
        IReadOnlySet<Guid> visibleIds,
        IReadOnlyDictionary<Guid, string> teamNames,
        TeamScope scope)
    {
        if (scope.SeesAllTeams)
            return [];

        return pathways
            .Where(p => !visibleIds.Contains(p.Id) && !NotSummarised.Contains(p.Status))
            .Select(p =>
            {
                var completed = p.Status == PatientPathwayStatus.Completed;
                // A completed pathway changes no more, so its last change is its completion.
                return new OtherTeamPathwaySummaryResponse(
                    teamNames.GetValueOrDefault(p.TeamId) ?? "another team",
                    completed ? Completed : InProgress,
                    DueDates.ToUkDate(completed ? p.LastUpdated : p.CreatedAt));
            })
            .OrderByDescending(s => s.Since)
            .ThenBy(s => s.TeamName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
