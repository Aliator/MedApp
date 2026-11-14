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
        var definition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                         ?? throw new NotFoundException($"Stage definition '{request.StageDefinitionId}' was not found.");

        if (request.Name?.Trim() is { } name && name != definition.Name)
        {
            if (await repository.StageDefinitionNameExistsAsync(name, definition.Id, cancellationToken))
                throw new ConflictException($"A stage called '{name}' already exists.");

            definition.Name = name;
        }

        if (request.Description is not null) definition.Description = request.Description;
        if (request.Instructions is not null) definition.Instructions = request.Instructions;

        await repository.UpdateStageDefinitionAsync(definition, cancellationToken);

        return await StageDefinitionMapper.ToResponseAsync(definition, repository, cancellationToken);
    }
}
