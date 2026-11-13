using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;

public sealed class UpdatePatientProcedureTemplateHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<UpdatePatientProcedureTemplateCommand, PatientProcedureStageTemplateResponse>
{
    public async Task<PatientProcedureStageTemplateResponse> Handle(
        UpdatePatientProcedureTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Patient procedure stage template '{request.TemplateId}' was not found.");

        if (request.Name is not null && !string.Equals(template.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var allTemplates = await repository.GetAllStageTemplatesAsync(cancellationToken);
            if (allTemplates.Any(x => x.Id != request.TemplateId && string.Equals(x.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ConflictException($"Patient procedure stage template name '{request.Name}' already exists.");

            template.Name = request.Name;
        }

        List<Guid>? stageDefinitionIds = null;
        if (request.StageDefinitionIdsInOrder is not null)
        {
            stageDefinitionIds = request.StageDefinitionIdsInOrder.Distinct().ToList();

            var allDefinitions = await repository.GetAllStageDefinitionsAsync(cancellationToken);
            var definitionsById = allDefinitions.ToDictionary(x => x.Id);
            var missingStageDefinitionIds = stageDefinitionIds
                .Where(id => !definitionsById.ContainsKey(id))
                .ToList();

            if (missingStageDefinitionIds.Count != 0)
                throw new NotFoundException($"Stage definition(s) not found: {string.Join(", ", missingStageDefinitionIds)}.");
        }

        await repository.UpdateStageTemplateAsync(template, cancellationToken);

        if (stageDefinitionIds is not null)
        {
            await repository.ReplaceStageTemplateMapsAsync(template.Id, stageDefinitionIds, cancellationToken);
            template.Maps = template.Maps
                .Where(x => stageDefinitionIds.Contains(x.StageDefinitionId))
                .OrderBy(x => stageDefinitionIds.IndexOf(x.StageDefinitionId))
                .ToList();
        }

        return new PatientProcedureStageTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            StageDefinitionIdsInOrder = stageDefinitionIds ?? template.Maps
                .OrderBy(x => x.StageOrder)
                .Select(x => x.StageDefinitionId)
                .ToList()
        };
    }
}