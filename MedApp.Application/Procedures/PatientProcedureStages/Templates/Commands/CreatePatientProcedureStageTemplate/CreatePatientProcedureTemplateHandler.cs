using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;

public sealed class CreatePatientProcedureTemplateHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<CreatePatientProcedureTemplateCommand, PatientProcedureStageTemplateResponse>
{
    public async Task<PatientProcedureStageTemplateResponse> Handle(
        CreatePatientProcedureTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var existingTemplates = await repository.GetAllStageTemplatesAsync(cancellationToken);
        if (existingTemplates.Any(x => string.Equals(x.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Patient procedure stage template name '{request.Name}' already exists.");

        var stageDefinitionIds = request.StageDefinitionIdsInOrder.Distinct().ToList();
        var allDefinitions = await repository.GetAllStageDefinitionsAsync(cancellationToken);
        var definitionsById = allDefinitions.ToDictionary(x => x.Id);

        var missingStageDefinitionIds = stageDefinitionIds
            .Where(id => !definitionsById.ContainsKey(id))
            .ToList();

        if (missingStageDefinitionIds.Count != 0)
            throw new NotFoundException($"Stage definition(s) not found: {string.Join(", ", missingStageDefinitionIds)}.");

        var now = DateTime.UtcNow;

        var template = new PatientProcedureStageTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
        };

        await repository.AddStageTemplateAsync(template, cancellationToken);
        await repository.ReplaceStageTemplateMapsAsync(template.Id, stageDefinitionIds, cancellationToken);

        return new PatientProcedureStageTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            StageDefinitionIdsInOrder = stageDefinitionIds
        };
    }
}