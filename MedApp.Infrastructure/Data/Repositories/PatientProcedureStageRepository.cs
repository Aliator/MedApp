using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PatientProcedureStagesRepository(MedAppDbContext context) : IPatientProcedureStagesRepository
{
    private DbSet<PatientProcedureStageDefinition> Definitions => context.Set<PatientProcedureStageDefinition>();
    private DbSet<PatientProcedureStageTemplate> Templates => context.Set<PatientProcedureStageTemplate>();
    private DbSet<PatientProcedureStageTemplateMap> Maps => context.Set<PatientProcedureStageTemplateMap>();

    public async Task AddStageDefinitionAsync(PatientProcedureStageDefinition definition, CancellationToken ct)
    {
        Definitions.Add(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientProcedureStageDefinition?> GetStageDefinitionByIdAsync(Guid id, CancellationToken ct) =>
        await Definitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PatientProcedureStageDefinition>> GetStageDefinitionsAsync(bool includeArchived, CancellationToken ct) =>
        await Definitions
            .AsNoTracking()
            .Where(x => includeArchived || !x.IsArchived)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PatientProcedureStageDefinition>> GetStageDefinitionsByIdAsync(IEnumerable<Guid> ids, CancellationToken ct)
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

        var procedureCounts = await context.Set<PatientProcedureStage>()
            .Where(s => list.Contains(s.StageDefinitionId))
            .GroupBy(s => s.StageDefinitionId)
            .Select(g => new { g.Key, Count = g.Select(s => s.PatientProcedureId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return list
            .Where(id => templateCounts.ContainsKey(id) || procedureCounts.ContainsKey(id))
            .ToDictionary(
                id => id,
                id => new StageDefinitionUsage(templateCounts.GetValueOrDefault(id), procedureCounts.GetValueOrDefault(id)));
    }

    public async Task UpdateStageDefinitionAsync(PatientProcedureStageDefinition definition, CancellationToken ct)
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

    public async Task AddStageTemplateAsync(PatientProcedureStageTemplate template, CancellationToken ct)
    {
        Templates.Add(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientProcedureStageTemplate?> GetStageTemplateByIdAsync(Guid id, CancellationToken ct) =>
        await WithStages(Templates.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PatientProcedureStageTemplate>> GetStageTemplatesAsync(bool includeArchived, CancellationToken ct) =>
        await WithStages(Templates.AsNoTracking())
            .Where(x => includeArchived || !x.IsArchived)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<bool> StageTemplateNameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await Templates.AnyAsync(x => x.Name.ToLower() == lower && (exceptId == null || x.Id != exceptId), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetTemplateProcedureCountsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();

        return await context.PatientProcedures
            .Where(p => p.TemplateId != null && list.Contains(p.TemplateId.Value))
            .GroupBy(p => p.TemplateId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task UpdateStageTemplateAsync(
        PatientProcedureStageTemplate template,
        IReadOnlyList<Guid>? stageDefinitionIdsInOrder,
        CancellationToken ct)
    {
        var existing = await Templates.Include(x => x.Maps).FirstOrDefaultAsync(x => x.Id == template.Id, ct);
        if (existing is null)
            return;

        existing.Name = template.Name;
        existing.IsArchived = template.IsArchived;

        if (stageDefinitionIdsInOrder is not null)
        {
            // Rows are keyed by position: reuse the positions that stay, add new ones, remove the rest.
            var byOrder = existing.Maps.ToDictionary(m => m.StageOrder);
            for (var i = 0; i < stageDefinitionIdsInOrder.Count; i++)
            {
                var order = i + 1;
                if (byOrder.TryGetValue(order, out var map))
                    map.StageDefinitionId = stageDefinitionIdsInOrder[i];
                else
                    Maps.Add(new PatientProcedureStageTemplateMap
                    {
                        TemplateId = existing.Id,
                        StageDefinitionId = stageDefinitionIdsInOrder[i],
                        StageOrder = order
                    });
            }

            Maps.RemoveRange(existing.Maps.Where(m => m.StageOrder > stageDefinitionIdsInOrder.Count).ToList());
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteStageTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await Templates.FindAsync([id], ct);
        if (template is null)
            return;

        // Its stage list cascades.
        Templates.Remove(template);
        await context.SaveChangesAsync(ct);
    }

    private static IQueryable<PatientProcedureStageTemplate> WithStages(IQueryable<PatientProcedureStageTemplate> templates) =>
        templates
            .Include(x => x.Maps.OrderBy(m => m.StageOrder))
            .ThenInclude(x => x.StageDefinition);
}
