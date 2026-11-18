using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.Insights.Responses;
using MediatR;

namespace MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;

/// <summary>
/// Gathers the insights' totals from the repository (each aggregated in SQL), one query after another as they share
/// a DbContext, then shapes them for the page (<see cref="PathwayInsightsMapper"/>).
/// </summary>
public sealed class GetPathwayInsightsHandler(
    IPathwayInsightsRepository repository,
    IPatientPathwayStagesRepository stagesRepository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetPathwayInsightsQuery, PathwayInsightsResponse>
{
    public async Task<PathwayInsightsResponse> Handle(GetPathwayInsightsQuery request, CancellationToken cancellationToken)
    {
        var templateId = request.TemplateId;
        if (templateId is { } id && await stagesRepository.GetPathwayTemplateByIdAsync(id, cancellationToken) is null)
            throw new NotFoundException($"Template '{id}' was not found.");

        var period = InsightsPeriod.For(timeProvider.GetUtcNow().UtcDateTime, request.Weeks);
        var now = period.NowUtc;

        var statusesNow = await repository.GetStatusCountsAsync(templateId, now, cancellationToken);
        var statusesLastWeek = await repository.GetStatusCountsAtAsync(templateId, period.SameMomentLastWeekUtc, cancellationToken);
        var completedThisWeek = await repository.CountCompletionsAsync(templateId, period.ThisWeek.StartUtc, now, cancellationToken);
        var completedLastWeek = await repository.CountCompletionsAsync(
            templateId, period.LastWeekStartUtc, period.SameMomentLastWeekUtc, cancellationToken);
        var (byAssignee, nobodyAssigned) = await repository.CountOverdueByAssigneeAsync(templateId, now, cancellationToken);
        var byPriority = await repository.CountOverdueByPriorityAsync(templateId, now, cancellationToken);
        var created = await repository.CountCreationsByHourAsync(templateId, period.StartUtc, now, cancellationToken);
        var completed = await repository.CountCompletionsByHourAsync(templateId, period.StartUtc, now, cancellationToken);
        var stageTimes = await repository.GetStageDurationsAsync(templateId, period.StartUtc, now, cancellationToken);

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

        return PathwayInsightsMapper.ToResponse(data, period, templateId, usernames);
    }
}
