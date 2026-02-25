using MedApp.Application.Tasks.Repositories;
using MedApp.Domain.Tasks.PatientTasks;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PatientTaskStagesRepository(MedAppDbContext context) : IPatientTaskStagesRepository
{
    public async Task AddStageDefinitionAsync(PatientTaskStageDefinition definition, CancellationToken ct)
    {
        context.Set<PatientTaskStageDefinition>().Add(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientTaskStageDefinition?> GetStageDefinitionByIdAsync(Guid id, CancellationToken ct)
    {
        return await context.Set<PatientTaskStageDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IEnumerable<PatientTaskStageDefinition>> GetAllStageDefinitionsAsync(CancellationToken ct)
    {
        return await context.Set<PatientTaskStageDefinition>()
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task UpdateStageDefinitionAsync(PatientTaskStageDefinition definition, CancellationToken ct)
    {
        context.Set<PatientTaskStageDefinition>().Update(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteStageDefinitionAsync(Guid id, CancellationToken ct)
    {
        var definition = await context.Set<PatientTaskStageDefinition>().FindAsync([id], ct);

        if (definition is null)
        {
            return;
        }

        context.Set<PatientTaskStageDefinition>().Remove(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddStageTemplateAsync(PatientTaskStageTemplate template, CancellationToken ct)
    {
        context.Set<PatientTaskStageTemplate>().Add(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientTaskStageTemplate?> GetStageTemplateByIdAsync(Guid id, CancellationToken ct)
    {
        var template = await context.Set<PatientTaskStageTemplate>()
            .AsNoTracking()
            .Include(x => x.Maps)
                .ThenInclude(x => x.StageDefinition)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (template is null)
        {
            return null;
        }

        template.Maps = template.Maps.OrderBy(x => x.StageOrder).ToList();
        return template;
    }

    public async Task<IEnumerable<PatientTaskStageTemplate>> GetAllStageTemplatesAsync(CancellationToken ct)
    {
        var templates = await context.Set<PatientTaskStageTemplate>()
            .AsNoTracking()
            .Include(x => x.Maps)
                .ThenInclude(x => x.StageDefinition)
            .ToListAsync(ct);

        foreach (var template in templates)
        {
            template.Maps = template.Maps.OrderBy(x => x.StageOrder).ToList();
        }

        return templates;
    }

    public async Task UpdateStageTemplateAsync(PatientTaskStageTemplate template, CancellationToken ct)
    {
        context.Set<PatientTaskStageTemplate>().Update(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteStageTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await context.Set<PatientTaskStageTemplate>().FindAsync([id], ct);

        if (template is null)
        {
            return;
        }

        context.Set<PatientTaskStageTemplate>().Remove(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task ReplaceStageTemplateMapsAsync(Guid templateId, IEnumerable<Guid> stageDefinitionIdsInOrder, CancellationToken ct)
    {
        var ids = stageDefinitionIdsInOrder.ToList();

        var existing = await context.Set<PatientTaskStageTemplateMap>()
            .Where(x => x.TemplateId == templateId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            context.Set<PatientTaskStageTemplateMap>().RemoveRange(existing);
        }

        var order = 1;
        var newMaps = new List<PatientTaskStageTemplateMap>(ids.Count);

        foreach (var stageDefinitionId in ids)
        {
            newMaps.Add(new PatientTaskStageTemplateMap
            {
                TemplateId = templateId,
                StageDefinitionId = stageDefinitionId,
                StageOrder = order++
            });
        }

        if (newMaps.Count > 0)
        {
            context.Set<PatientTaskStageTemplateMap>().AddRange(newMaps);
        }

        await context.SaveChangesAsync(ct);
    }
}