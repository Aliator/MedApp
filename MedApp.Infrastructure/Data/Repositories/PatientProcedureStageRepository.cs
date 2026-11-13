using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PatientProcedureStagesRepository(MedAppDbContext context) : IPatientProcedureStagesRepository
{
    public async Task AddStageDefinitionAsync(PatientProcedureStageDefinition definition, CancellationToken ct)
    {
        context.Set<PatientProcedureStageDefinition>().Add(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientProcedureStageDefinition?> GetStageDefinitionByIdAsync(Guid id, CancellationToken ct)
    {
        return await context.Set<PatientProcedureStageDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IEnumerable<PatientProcedureStageDefinition>> GetAllStageDefinitionsAsync(CancellationToken ct)
    {
        return await context.Set<PatientProcedureStageDefinition>()
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task UpdateStageDefinitionAsync(PatientProcedureStageDefinition definition, CancellationToken ct)
    {
        context.Set<PatientProcedureStageDefinition>().Update(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteStageDefinitionAsync(Guid id, CancellationToken ct)
    {
        var definition = await context.Set<PatientProcedureStageDefinition>().FindAsync([id], ct);

        if (definition is null)
        {
            return;
        }

        context.Set<PatientProcedureStageDefinition>().Remove(definition);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddStageTemplateAsync(PatientProcedureStageTemplate template, CancellationToken ct)
    {
        context.Set<PatientProcedureStageTemplate>().Add(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientProcedureStageTemplate?> GetStageTemplateByIdAsync(Guid id, CancellationToken ct)
    {
        var template = await context.Set<PatientProcedureStageTemplate>()
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

    public async Task<IEnumerable<PatientProcedureStageTemplate>> GetAllStageTemplatesAsync(CancellationToken ct)
    {
        var templates = await context.Set<PatientProcedureStageTemplate>()
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

    public async Task UpdateStageTemplateAsync(PatientProcedureStageTemplate template, CancellationToken ct)
    {
        context.Set<PatientProcedureStageTemplate>().Update(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteStageTemplateAsync(Guid id, CancellationToken ct)
    {
        var template = await context.Set<PatientProcedureStageTemplate>().FindAsync([id], ct);

        if (template is null)
        {
            return;
        }

        context.Set<PatientProcedureStageTemplate>().Remove(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task ReplaceStageTemplateMapsAsync(Guid templateId, IEnumerable<Guid> stageDefinitionIdsInOrder, CancellationToken ct)
    {
        var ids = stageDefinitionIdsInOrder.ToList();

        var existing = await context.Set<PatientProcedureStageTemplateMap>()
            .Where(x => x.TemplateId == templateId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            context.Set<PatientProcedureStageTemplateMap>().RemoveRange(existing);
        }

        var order = 1;
        var newMaps = new List<PatientProcedureStageTemplateMap>(ids.Count);

        foreach (var stageDefinitionId in ids)
        {
            newMaps.Add(new PatientProcedureStageTemplateMap
            {
                TemplateId = templateId,
                StageDefinitionId = stageDefinitionId,
                StageOrder = order++
            });
        }

        if (newMaps.Count > 0)
        {
            context.Set<PatientProcedureStageTemplateMap>().AddRange(newMaps);
        }

        await context.SaveChangesAsync(ct);
    }
}