using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetAllPatientTaskStageDefinitions;

public sealed class GetAllPatientTaskStagesHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<GetAllPatientTaskStagesQuery, IReadOnlyList<PatientTaskStageDefinitionResponse>>
{
    public async Task<IReadOnlyList<PatientTaskStageDefinitionResponse>> Handle(
        GetAllPatientTaskStagesQuery request,
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