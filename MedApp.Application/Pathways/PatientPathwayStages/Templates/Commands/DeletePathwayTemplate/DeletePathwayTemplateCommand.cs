using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.DeletePathwayTemplate;

/// <summary>Deletes a template that no pathway was created from; one that has been used is archived instead.</summary>
public sealed record DeletePathwayTemplateCommand(Guid TemplateId) : IRequest;
