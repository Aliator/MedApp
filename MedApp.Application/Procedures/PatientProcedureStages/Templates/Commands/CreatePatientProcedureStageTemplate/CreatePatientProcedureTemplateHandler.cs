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
        var name = request.Name.Trim();
        if (await repository.StageTemplateNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A template called '{name}' already exists.");

        var definitions = await TemplateStages.CheckAsync(
            repository, request.StageDefinitionIdsInOrder, new HashSet<Guid>(), cancellationToken);

        var templateId = Guid.NewGuid();
        var template = new PatientProcedureStageTemplate
        {
            Id = templateId,
            Name = name,
            Maps = StageTemplateMapper.ToMaps(templateId, request.StageDefinitionIdsInOrder)
        };

        await repository.AddStageTemplateAsync(template, cancellationToken);

        return StageTemplateMapper.ToResponse(template, 0, definitions);
    }
}
