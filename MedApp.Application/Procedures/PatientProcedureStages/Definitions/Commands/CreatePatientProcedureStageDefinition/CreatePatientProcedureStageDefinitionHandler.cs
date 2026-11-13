using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;

public sealed class CreatePatientProcedureStageDefinitionHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<CreatePatientProcedureStageDefinitionCommand, PatientProcedureStageDefinitionResponse>
{
    public async Task<PatientProcedureStageDefinitionResponse> Handle(
        CreatePatientProcedureStageDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var stageDefinition = new PatientProcedureStageDefinition
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Instructions = request.Instructions,
        };

        await repository.AddStageDefinitionAsync(stageDefinition, cancellationToken);

        return new PatientProcedureStageDefinitionResponse
        {
            Id = stageDefinition.Id,
            Name = stageDefinition.Name,
            Description = stageDefinition.Description,
            Instructions = stageDefinition.Instructions,
        };
    }
}