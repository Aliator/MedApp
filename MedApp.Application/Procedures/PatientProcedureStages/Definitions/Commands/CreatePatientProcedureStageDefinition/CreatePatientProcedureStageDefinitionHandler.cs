using MedApp.Application.Common.Exceptions;
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
        var name = request.Name.Trim();
        if (await repository.StageDefinitionNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A stage called '{name}' already exists.");

        var definition = new PatientProcedureStageDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description,
            Instructions = request.Instructions
        };

        await repository.AddStageDefinitionAsync(definition, cancellationToken);

        return StageDefinitionMapper.ToResponse(definition, StageDefinitionUsage.None);
    }
}
