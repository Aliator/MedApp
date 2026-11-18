using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

public sealed class CreatePathwayTemplateHandler(
    IPatientPathwayStagesRepository repository,
    ISpecialtyRepository specialties)
    : IRequestHandler<CreatePathwayTemplateCommand, PathwayTemplateResponse>
{
    public async Task<PathwayTemplateResponse> Handle(
        CreatePathwayTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.PathwayTemplateNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A template called '{name}' already exists.");

        var definitions = await TemplateStages.CheckAsync(
            repository, request.StageDefinitionIdsInOrder, new HashSet<Guid>(), cancellationToken);

        // An archived specialty can't be chosen (409); one that doesn't exist is a 404.
        var specialty = (await SpecialtyRules.CheckChosenAsync(
            specialties, [request.SpecialtyId], new HashSet<Guid>(), cancellationToken))[request.SpecialtyId];

        var templateId = Guid.NewGuid();
        var template = new PathwayTemplate
        {
            Id = templateId,
            Name = name,
            SpecialtyId = specialty.Id,
            Specialty = specialty,
            Maps = PathwayTemplateMapper.ToMaps(templateId, request.StageDefinitionIdsInOrder, request.ClockStopStageOrder)
        };

        await repository.AddPathwayTemplateAsync(template, cancellationToken);

        return PathwayTemplateMapper.ToResponse(template, 0, definitions);
    }
}
