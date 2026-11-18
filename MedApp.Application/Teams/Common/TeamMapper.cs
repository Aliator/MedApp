using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Teams;

namespace MedApp.Application.Teams.Common;

public static class TeamMapper
{
    /// <summary>A team with what has it; it can be deleted only while nothing does.</summary>
    public static TeamResponse ToResponse(Team team, TeamUsage usage) => new()
    {
        Id = team.Id,
        Name = team.Name,
        IsArchived = team.IsArchived,
        Version = team.Version,
        TemplateCount = usage.TemplateCount,
        PathwayCount = usage.PathwayCount,
        MemberCount = usage.MemberCount,
        ReferralCount = usage.ReferralCount,
        LetterTemplateCount = usage.LetterTemplateCount,
        TriagerCount = usage.TriagerCount,
        CanBeDeleted = !usage.IsInUse,
        TargetDaysUrgent = team.TargetDaysUrgent,
        TargetDaysHigh = team.TargetDaysHigh,
        TargetDaysMedium = team.TargetDaysMedium,
        TargetDaysLow = team.TargetDaysLow,
        CreatedAt = team.CreatedAt,
        LastUpdated = team.LastUpdated
    };

    /// <summary>A team's response, looking up what has it.</summary>
    public static async Task<TeamResponse> ToResponseAsync(
        Team team, ITeamRepository repository, CancellationToken ct)
    {
        var usage = await repository.GetUsageAsync([team.Id], ct);
        return ToResponse(team, usage.GetValueOrDefault(team.Id, TeamUsage.None));
    }

    /// <summary>A team someone belongs to.</summary>
    public static TeamMembershipResponse ToStaffResponse(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        IsArchived = team.IsArchived
    };

    /// <summary>The teams someone belongs to, sorted by name.</summary>
    public static IReadOnlyList<TeamMembershipResponse> ToStaffResponses(IEnumerable<Team> teams) =>
        teams
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id)
            .Select(ToStaffResponse)
            .ToList();
}
