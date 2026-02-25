using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Queries.GetAllPatientTaskStageTemplates;

public sealed class GetAllPatientTaskTemplatesHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<GetAllPatientTaskTemplatesQuery, IReadOnlyList<PatientTaskStageTemplateResponse>>
{
    public async Task<IReadOnlyList<PatientTaskStageTemplateResponse>> Handle(
        GetAllPatientTaskTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await repository.GetAllStageTemplatesAsync(cancellationToken);

        return templates
            .OrderBy(x => x.Name)
            .Select(x => new PatientTaskStageTemplateResponse
            {
                Id = x.Id,
                Name = x.Name,
                StageDefinitionIdsInOrder = x.Maps
                    .OrderBy(map => map.StageOrder)
                    .Select(map => map.StageDefinitionId)
                    .ToList()
            })
            .ToList();
    }
}