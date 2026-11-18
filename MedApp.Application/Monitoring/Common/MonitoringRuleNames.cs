using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Monitoring;

namespace MedApp.Application.Monitoring.Common;

/// <summary>
/// The names, now, of the people, templates and stages some rules name, for their responses. Each lookup leaves out
/// what no longer exists (a deleted account, template or stage).
/// </summary>
public sealed record MonitoringRuleNames(
    IReadOnlyDictionary<Guid, string> Usernames,
    IReadOnlyDictionary<Guid, string> TemplateNames,
    IReadOnlyDictionary<Guid, string> StageNames,
    IReadOnlyDictionary<Guid, string> SpecialtyNames)
{
    /// <summary>No names at all.</summary>
    public static MonitoringRuleNames None { get; } = new(
        new Dictionary<Guid, string>(), new Dictionary<Guid, string>(), new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

    /// <summary>Looks up everything these rules name, in four queries (or none, for what none of them names).</summary>
    public static async Task<MonitoringRuleNames> LoadAsync(
        IReadOnlyCollection<MonitoringRule> rules,
        IIdentityReadService identityReadService,
        IPatientPathwayStagesRepository stagesRepository,
        ISpecialtyRepository specialtyRepository,
        CancellationToken cancellationToken)
    {
        var userIds = rules.Select(r => r.SubjectUserId).OfType<Guid>().Distinct().ToList();
        var templateIds = rules.Select(r => r.TemplateId).OfType<Guid>().Distinct().ToList();
        var stageIds = rules.Select(r => r.StageDefinitionId).OfType<Guid>().Distinct().ToList();
        var specialtyIds = rules.Select(r => r.SpecialtyId).OfType<Guid>().Distinct().ToList();

        var usernames = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await identityReadService.GetUsernamesByIdAsync(userIds, cancellationToken);
        var templates = templateIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await stagesRepository.GetPathwayTemplateNamesAsync(templateIds, cancellationToken);
        var stages = stageIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await stagesRepository.GetStageDefinitionsByIdAsync(stageIds, cancellationToken)).ToDictionary(d => d.Id, d => d.Name);

        var specialties = specialtyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await specialtyRepository.GetByIdsAsync(specialtyIds, cancellationToken)).ToDictionary(s => s.Id, s => s.Name);

        return new MonitoringRuleNames(usernames, templates, stages, specialties);
    }
}
