using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.EditPathwayNote;

/// <summary>
/// Replaces the text of a note on a pathway: only its author can, while the pathway's notes can change, and only from
/// the version they saw (<paramref name="ExpectedVersion"/>; otherwise a 409).
/// </summary>
public sealed record EditPathwayNoteCommand(Guid PathwayId, Guid NoteId, string Text, int ExpectedVersion, PathwayActor Actor)
    : IRequest<PatientPathwayNoteResponse>;
