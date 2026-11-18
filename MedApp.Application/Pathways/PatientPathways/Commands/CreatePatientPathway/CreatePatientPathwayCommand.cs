using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;

/// <summary>
/// Starts a pathway for a patient from a template, copying the template's stages. A blank title uses the template's
/// name. Notes, if any, become its first note, by the actor.
/// </summary>
public sealed record CreatePatientPathwayCommand(
    Guid PatientId,
    Guid TemplateId,
    string? Title,
    string Notes,
    DateOnly DueDate,
    string Priority,
    PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
