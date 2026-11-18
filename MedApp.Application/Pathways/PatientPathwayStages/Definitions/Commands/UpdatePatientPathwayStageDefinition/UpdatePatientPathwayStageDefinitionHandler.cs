using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.UpdatePatientPathwayStageDefinition;

public sealed class UpdatePatientPathwayStageDefinitionHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<UpdatePatientPathwayStageDefinitionCommand, PatientPathwayStageDefinitionResponse>
{
    public async Task<PatientPathwayStageDefinitionResponse> Handle(
        UpdatePatientPathwayStageDefinitionCommand request,
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
        if (request.SnomedCode is not null)
            (definition.SnomedCode, definition.SnomedTerm) = SnomedRules.Normalise(request.SnomedCode, request.SnomedTerm);
        if (request.VisitRequirement is not null)
            definition.VisitRequirement = PathwayEnums.Parse<VisitRequirement>(request.VisitRequirement);
        if (request.NeedsLetterKind is not null)
            definition.NeedsLetterKind = StageLetterKinds.Parse(request.NeedsLetterKind);

        await repository.UpdateStageDefinitionAsync(definition, cancellationToken);

        return await StageDefinitionMapper.ToResponseAsync(definition, repository, cancellationToken);
    }
}
