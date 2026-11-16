using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Insights.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.Insights.Responses;
using MediatR;

namespace MedApp.Application.Procedures.Insights.Queries.GetProcedureInsights;

/// <summary>
/// Gathers the insights' totals from the repository (each aggregated in SQL), one query after another as they share
/// a DbContext, then shapes them for the page (<see cref="ProcedureInsightsMapper"/>).
/// </summary>
public sealed class GetProcedureInsightsHandler(
    IProcedureInsightsRepository repository,
    IPatientProcedureStagesRepository stagesRepository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetProcedureInsightsQuery, ProcedureInsightsResponse>
{
    public async Task<ProcedureInsightsResponse> Handle(GetProcedureInsightsQuery request, CancellationToken cancellationToken)
    {
        var templateId = request.TemplateId;
        if (templateId is { } id && await stagesRepository.GetStageTemplateByIdAsync(id, cancellationToken) is null)
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

        return ProcedureInsightsMapper.ToResponse(data, period, templateId, usernames);
    }
}
