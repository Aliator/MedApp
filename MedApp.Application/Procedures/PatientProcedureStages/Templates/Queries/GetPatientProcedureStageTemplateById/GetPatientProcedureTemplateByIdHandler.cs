using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetPatientProcedureStageTemplateById;

public sealed class GetPatientProcedureTemplateByIdHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<GetPatientProcedureTemplateByIdQuery, PatientProcedureStageTemplateResponse>
{
    public async Task<PatientProcedureStageTemplateResponse> Handle(
        GetPatientProcedureTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Patient procedure stage template '{request.TemplateId}' was not found.");

        return new PatientProcedureStageTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            StageDefinitionIdsInOrder = template.Maps
                .OrderBy(x => x.StageOrder)
                .Select(x => x.StageDefinitionId)
                .ToList()
        };
    }
}