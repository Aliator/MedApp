using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

public sealed class UpdatePatientProcedureStageDefinitionHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<UpdatePatientProcedureStageDefinitionCommand, PatientProcedureStageDefinitionResponse>
{
    public async Task<PatientProcedureStageDefinitionResponse> Handle(
        UpdatePatientProcedureStageDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var stageDefinition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                              ?? throw new NotFoundException($"Patient procedure stage definition '{request.StageDefinitionId}' was not found.");

        if (request.Name is not null) stageDefinition.Name = request.Name;
        if (request.Description is not null) stageDefinition.Description = request.Description;
        if (request.Instructions is not null) stageDefinition.Instructions = request.Instructions;

        await repository.UpdateStageDefinitionAsync(stageDefinition, cancellationToken);

        return new PatientProcedureStageDefinitionResponse
        {
            Id = stageDefinition.Id,
            Name = stageDefinition.Name,
            Description = stageDefinition.Description,
            Instructions = stageDefinition.Instructions
        };
    }
}