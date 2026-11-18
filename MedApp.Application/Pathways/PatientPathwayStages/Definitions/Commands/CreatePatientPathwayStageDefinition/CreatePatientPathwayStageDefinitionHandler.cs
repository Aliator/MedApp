using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.CreatePatientPathwayStageDefinition;

public sealed class CreatePatientPathwayStageDefinitionHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<CreatePatientPathwayStageDefinitionCommand, PatientPathwayStageDefinitionResponse>
{
    public async Task<PatientPathwayStageDefinitionResponse> Handle(
        CreatePatientPathwayStageDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.StageDefinitionNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A stage called '{name}' already exists.");

        var definition = new PatientPathwayStageDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description,
            Instructions = request.Instructions,
            VisitRequirement = PathwayEnums.Parse<VisitRequirement>(request.VisitRequirement),
            NeedsLetterKind = StageLetterKinds.Parse(request.NeedsLetterKind),
            WaitsForResults = request.WaitsForResults
        };
        (definition.SnomedCode, definition.SnomedTerm) = SnomedRules.Normalise(request.SnomedCode, request.SnomedTerm);

        await repository.AddStageDefinitionAsync(definition, cancellationToken);

        return StageDefinitionMapper.ToResponse(definition, StageDefinitionUsage.None);
    }
}
