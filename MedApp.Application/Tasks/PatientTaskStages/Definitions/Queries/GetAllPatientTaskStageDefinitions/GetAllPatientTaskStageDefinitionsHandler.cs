using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetAllPatientTaskStageDefinitions;

public sealed class GetAllPatientTaskStageDefinitionsHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<GetAllPatientTaskStageDefinitionsQuery, IReadOnlyList<PatientTaskStageDefinitionResponse>>
{
    public async Task<IReadOnlyList<PatientTaskStageDefinitionResponse>> Handle(
        GetAllPatientTaskStageDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        var stageDefinitions = await repository.GetAllStageDefinitionsAsync(cancellationToken);

        return stageDefinitions
            .Select(x => new PatientTaskStageDefinitionResponse
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Instructions = x.Instructions
            })
            .ToList();
    }
}