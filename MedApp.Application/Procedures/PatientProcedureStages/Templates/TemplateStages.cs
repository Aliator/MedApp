using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates;

public static class TemplateStages
{
    /// <summary>
    /// Checks a template's new stage list: every definition must exist (404), and archived ones may only stay where
    /// the template already had them (409). Returns the definitions by ID.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, PatientProcedureStageDefinition>> CheckAsync(
        IPatientProcedureStagesRepository repository,
        IReadOnlyList<Guid> stageDefinitionIdsInOrder,
        IReadOnlySet<Guid> alreadyInTemplate,
        CancellationToken ct)
    {
        var ids = stageDefinitionIdsInOrder.Distinct().ToList();
        var definitions = (await repository.GetStageDefinitionsByIdAsync(ids, ct)).ToDictionary(d => d.Id);

        var missing = ids.Where(id => !definitions.ContainsKey(id)).ToList();
        if (missing.Count != 0)
            throw new NotFoundException($"Stage definition(s) not found: {string.Join(", ", missing)}.");

        var archived = ids
            .Where(id => definitions[id].IsArchived && !alreadyInTemplate.Contains(id))
            .Select(id => definitions[id].Name)
            .ToList();
        if (archived.Count != 0)
            throw new ConflictException($"Archived stages can't be added to a template: {string.Join(", ", archived)}.");

        return definitions;
    }
}
