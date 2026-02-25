using MedApp.Application.Common.Exceptions;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStages.Templates.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.CreatePatientTaskStageTemplate;

public sealed class CreatePatientTaskTemplateHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<CreatePatientTaskTemplateCommand, PatientTaskStageTemplateResponse>
{
    public async Task<PatientTaskStageTemplateResponse> Handle(
        CreatePatientTaskTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var existingTemplates = await repository.GetAllStageTemplatesAsync(cancellationToken);
        if (existingTemplates.Any(x => string.Equals(x.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Patient task stage template name '{request.Name}' already exists.");

        var stageDefinitionIds = request.StageDefinitionIdsInOrder.Distinct().ToList();
        var allDefinitions = await repository.GetAllStageDefinitionsAsync(cancellationToken);
        var definitionsById = allDefinitions.ToDictionary(x => x.Id);

        var missingStageDefinitionIds = stageDefinitionIds
            .Where(id => !definitionsById.ContainsKey(id))
            .ToList();

        if (missingStageDefinitionIds.Count != 0)
            throw new NotFoundException($"Stage definition(s) not found: {string.Join(", ", missingStageDefinitionIds)}.");

        var now = DateTime.UtcNow;

        var template = new PatientTaskStageTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CreatedAt = now,
            LastUpdated = now
        };

        await repository.AddStageTemplateAsync(template, cancellationToken);
        await repository.ReplaceStageTemplateMapsAsync(template.Id, stageDefinitionIds, cancellationToken);

        return new PatientTaskStageTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            CreatedAt = template.CreatedAt,
            LastUpdated = template.LastUpdated,
            StageDefinitionIdsInOrder = stageDefinitionIds
        };
    }
}