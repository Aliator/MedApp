using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
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
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        if (request.Name?.Trim() is { } name && name != template.Name)
        {
            if (await repository.StageTemplateNameExistsAsync(name, template.Id, cancellationToken))
                throw new ConflictException($"A template called '{name}' already exists.");

            template.Name = name;
        }

        IReadOnlyDictionary<Guid, PatientProcedureStageDefinition>? definitions = null;
        if (request.StageDefinitionIdsInOrder is { } ids)
        {
            var current = template.Maps.Select(m => m.StageDefinitionId).ToHashSet();
            definitions = await TemplateStages.CheckAsync(repository, ids, current, cancellationToken);
        }

        await repository.UpdateStageTemplateAsync(template, request.StageDefinitionIdsInOrder, cancellationToken);

        if (request.StageDefinitionIdsInOrder is { } newIds)
            template.Maps = StageTemplateMapper.ToMaps(template.Id, newIds);

        var counts = await repository.GetTemplateProcedureCountsAsync([template.Id], cancellationToken);
        return StageTemplateMapper.ToResponse(template, counts.GetValueOrDefault(template.Id), definitions);
    }
}
