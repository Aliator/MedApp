using MedApp.Application.Common.Exceptions;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Queries.GetPatientTaskStageTemplateById;

public sealed class GetPatientTaskTemplateByIdHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<GetPatientTaskTemplateByIdQuery, PatientTaskStageTemplateResponse>
{
    public async Task<PatientTaskStageTemplateResponse> Handle(
        GetPatientTaskTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Patient task stage template '{request.TemplateId}' was not found.");

        return new PatientTaskStageTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            CreatedAt = template.CreatedAt,
            LastUpdated = template.LastUpdated,
            StageDefinitionIdsInOrder = template.Maps
                .OrderBy(x => x.StageOrder)
                .Select(x => x.StageDefinitionId)
                .ToList()
        };
    }
}