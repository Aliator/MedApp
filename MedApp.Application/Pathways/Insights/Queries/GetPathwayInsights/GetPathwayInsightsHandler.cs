using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Pathways.Insights.Responses;
using MediatR;

namespace MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;

/// <summary>
/// Gathers the insights' totals from the repository (each aggregated in SQL), one query after another as they share
/// a DbContext, then shapes them for the page (<see cref="PathwayInsightsMapper"/>). Only the viewer's teams' pathways
/// count (an admin's: every team's, or the one picked); a team picked that isn't theirs is a 400 on <c>TeamId</c>
/// (<see cref="TeamPicker"/>).
/// </summary>
public sealed class GetPathwayInsightsHandler(
    IPathwayInsightsRepository repository,
    IPatientPathwayStagesRepository stagesRepository,
    ITeamRepository teamRepository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetPathwayInsightsQuery, PathwayInsightsResponse>
{
    public async Task<PathwayInsightsResponse> Handle(GetPathwayInsightsQuery request, CancellationToken cancellationToken)
    {
        TeamPicker.EnsureCanPick(request.TeamId, request.Teams);
        if (request.TemplateId is { } templateId && await stagesRepository.GetPathwayTemplateByIdAsync(templateId, cancellationToken) is null)
            throw new NotFoundException($"Template '{templateId}' was not found.");
        if (request.TeamId is { } teamId && await teamRepository.GetAsync(teamId, cancellationToken) is null)
            throw new NotFoundException($"Team '{teamId}' was not found.");

        var scope = new InsightsScope(request.TemplateId, request.TeamId, InsightsScope.TeamsOf(request.Teams));

        var period = InsightsPeriod.For(timeProvider.GetUtcNow().UtcDateTime, request.Weeks);
        var now = period.NowUtc;

        var statusesNow = await repository.GetStatusCountsAsync(scope, now, cancellationToken);
        var statusesLastWeek = await repository.GetStatusCountsAtAsync(scope, period.SameMomentLastWeekUtc, cancellationToken);
        var completedThisWeek = await repository.CountCompletionsAsync(scope, period.ThisWeek.StartUtc, now, cancellationToken);
        var completedLastWeek = await repository.CountCompletionsAsync(
            scope, period.LastWeekStartUtc, period.SameMomentLastWeekUtc, cancellationToken);
        var (byAssignee, nobodyAssigned) = await repository.CountOverdueByAssigneeAsync(scope, now, cancellationToken);
        var byPriority = await repository.CountOverdueByPriorityAsync(scope, now, cancellationToken);
        var created = await repository.CountCreationsByHourAsync(scope, period.StartUtc, now, cancellationToken);
        var completed = await repository.CountCompletionsByHourAsync(scope, period.StartUtc, now, cancellationToken);
        var stageTimes = await repository.GetStageDurationsAsync(scope, period.StartUtc, now, cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync(
            byAssignee.Select(a => a.UserId).Distinct().ToList(), cancellationToken);

        var data = new InsightsData(
            statusesNow,
            statusesLastWeek,
            completedThisWeek,
            completedLastWeek,
            byAssignee,
            nobodyAssigned,
            byPriority,
            created,
            completed,
            stageTimes);

        return PathwayInsightsMapper.ToResponse(data, period, scope, usernames);
    }
}
