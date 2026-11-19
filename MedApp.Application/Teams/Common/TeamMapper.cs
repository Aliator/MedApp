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
        Kind = team.Kind.ToString(),
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

    /// <summary>A team someone belongs to, with its kind and their role in it.</summary>
    public static TeamMembershipResponse ToStaffResponse(Team team, TeamMemberRole role = TeamMemberRole.Member) => new()
    {
        Id = team.Id,
        Name = team.Name,
        IsArchived = team.IsArchived,
        Kind = team.Kind.ToString(),
        Role = role.ToString()
    };

    /// <summary>The teams someone belongs to, as members, sorted by name.</summary>
    public static IReadOnlyList<TeamMembershipResponse> ToStaffResponses(IEnumerable<Team> teams) =>
        teams
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id)
            .Select(t => ToStaffResponse(t))
            .ToList();

    /// <summary>Someone's memberships, each with its team's kind and their role, sorted by the team's name.</summary>
    public static IReadOnlyList<TeamMembershipResponse> ToStaffResponses(IEnumerable<TeamMembership> memberships) =>
        memberships
            .OrderBy(m => m.Team.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.TeamId)
            .Select(m => ToStaffResponse(m.Team, m.Role))
            .ToList();

    /// <summary>
    /// A membership as the activity log names it: the team's name, and "(clinic admin)" for a clinic admin (as the light
    /// demo writes it).
    /// </summary>
    public static string ActivityName(TeamMembershipResponse membership) =>
        membership.Role == nameof(TeamMemberRole.ClinicAdmin) ? $"{membership.Name} (clinic admin)" : membership.Name;
}
