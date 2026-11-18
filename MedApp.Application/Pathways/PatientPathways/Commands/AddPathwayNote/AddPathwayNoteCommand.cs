using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;

/// <summary>Adds a note to a pathway, by the actor. Allowed whenever its notes can change (not Cancelled or Archived).</summary>
public sealed record AddPathwayNoteCommand(Guid PathwayId, string Text, PathwayActor Actor)
    : IRequest<PatientPathwayNoteResponse>;
