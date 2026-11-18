using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.SetPathwayTemplateArchived;

/// <summary>Archives a template (it can't be used for new pathways) or restores it.</summary>
public sealed record SetPathwayTemplateArchivedCommand(Guid TemplateId, bool IsArchived)
    : IRequest<PathwayTemplateResponse>;
