using MedApp.Application.Pathways.PatientPathwayStages.Templates;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PatientPathwayStagesRepository(MedAppDbContext context) : IPatientPathwayStagesRepository
{
    private DbSet<PatientPathwayStageDefinition> Definitions => context.Set<PatientPathwayStageDefinition>();
    private DbSet<PathwayTemplate> Templates => context.Set<PathwayTemplate>();
    private DbSet<PathwayTemplateMap> Maps => context.Set<PathwayTemplateMap>();

    public async Task AddStageDefinitionAsync(PatientPathwayStageDefinition definition, CancellationToken ct)
    {
        Definitions.Add(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientPathwayStageDefinition?> GetStageDefinitionByIdAsync(Guid id, CancellationToken ct) =>
        await Definitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PatientPathwayStageDefinition>> GetStageDefinitionsAsync(bool includeArchived, CancellationToken ct) =>
        await Definitions
            .AsNoTracking()
            .Where(x => includeArchived || !x.IsArchived)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PatientPathwayStageDefinition>> GetStageDefinitionsByIdAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await Definitions.AsNoTracking().Where(x => list.Contains(x.Id)).ToListAsync(ct);
    }

    public async Task<bool> StageDefinitionNameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await Definitions.AnyAsync(x => x.Name.ToLower() == lower && (exceptId == null || x.Id != exceptId), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, StageDefinitionUsage>> GetStageDefinitionUsageAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();

        var templateCounts = await Maps
            .Where(m => list.Contains(m.StageDefinitionId))
            .GroupBy(m => m.StageDefinitionId)
            .Select(g => new { g.Key, Count = g.Select(m => m.TemplateId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var pathwayCounts = await context.Set<PatientPathwayStage>()
            .Where(s => list.Contains(s.StageDefinitionId))
            .GroupBy(s => s.StageDefinitionId)
            .Select(g => new { g.Key, Count = g.Select(s => s.PatientPathwayId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return list
            .Where(id => templateCounts.ContainsKey(id) || pathwayCounts.ContainsKey(id))
            .ToDictionary(
                id => id,
                id => new StageDefinitionUsage(templateCounts.GetValueOrDefault(id), pathwayCounts.GetValueOrDefault(id)));
    }

    public async Task UpdateStageDefinitionAsync(PatientPathwayStageDefinition definition, CancellationToken ct)
    {
        Definitions.Update(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteStageDefinitionAsync(Guid id, CancellationToken ct)
    {
        var definition = await Definitions.FindAsync([id], ct);
        if (definition is null)
            return;

        Definitions.Remove(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddPathwayTemplateAsync(PathwayTemplate template, CancellationToken ct)
    {
        Templates.Add(template);
        // The specialty it's in already exists: attached (if given) for the response's name, never inserted.
        if (template.Specialty is not null)
            context.Entry(template.Specialty).State = EntityState.Unchanged;
        await context.SaveChangesAsync(ct);
    }

    public async Task<PathwayTemplate?> GetPathwayTemplateByIdAsync(Guid id, CancellationToken ct) =>
        await WithStages(Templates.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PathwayTemplate>> GetPathwayTemplatesAsync(bool includeArchived, CancellationToken ct) =>
        await WithStages(Templates.AsNoTracking())
            .Where(x => includeArchived || !x.IsArchived)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetPathwayTemplateNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return new Dictionary<Guid, string>();

        return await Templates
            .Where(x => list.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    }

    public async Task<bool> PathwayTemplateNameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await Templates.AnyAsync(x => x.Name.ToLower() == lower && (exceptId == null || x.Id != exceptId), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetTemplatePathwayCountsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();

        return await context.PatientPathways
            .Where(p => p.TemplateId != null && list.Contains(p.TemplateId.Value))
            .GroupBy(p => p.TemplateId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task UpdatePathwayTemplateAsync(
        PathwayTemplate template,
        IReadOnlyList<Guid>? stageDefinitionIdsInOrder,
        int? clockStopStageOrder,
        IReadOnlyList<TemplateStageOptions>? stageOptions,
        CancellationToken ct)
    {
        var existing = await Templates.Include(x => x.Maps).FirstOrDefaultAsync(x => x.Id == template.Id, ct);
        if (existing is null)
            return;

        existing.Name = template.Name;
        existing.IsArchived = template.IsArchived;
        existing.SpecialtyId = template.SpecialtyId;

        if (stageDefinitionIdsInOrder is not null)
        {
            // Rows are keyed by position: reuse the positions that stay, add new ones, remove the rest.
            var byOrder = existing.Maps.ToDictionary(m => m.StageOrder);
            var newMaps = PathwayTemplateMapper.ToMaps(existing.Id, stageDefinitionIdsInOrder, clockStopStageOrder, stageOptions);
            foreach (var newMap in newMaps)
            {
                if (byOrder.TryGetValue(newMap.StageOrder, out var map))
                {
                    map.StageDefinitionId = newMap.StageDefinitionId;
                    map.IsOptional = newMap.IsOptional;
                    map.ChoosesBranch = newMap.ChoosesBranch;
                    map.Branch = newMap.Branch;
                    map.StopsClock = newMap.StopsClock;
                }
                else
                {
                    Maps.Add(newMap);
                }
            }

            Maps.RemoveRange(existing.Maps.Where(m => m.StageOrder > stageDefinitionIdsInOrder.Count).ToList());
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeletePathwayTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await Templates.FindAsync([id], ct);
        if (template is null)
            return;

        // Its stage list cascades.
        Templates.Remove(template);
        await context.SaveChangesAsync(ct);
    }

    private static IQueryable<PathwayTemplate> WithStages(IQueryable<PathwayTemplate> templates) =>
        templates
            .Include(x => x.Specialty)
            .Include(x => x.Maps.OrderBy(m => m.StageOrder))
            .ThenInclude(x => x.StageDefinition);
}
