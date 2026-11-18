using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.NoteDeletionRequests.Commands.RequestNoteDeletion;

/// <summary>
/// Asks an admin to delete a note on a pathway, with a reason (a <c>NoteDeletionReason</c>, by name) and an optional
/// detail: any note on a pathway the member of staff can see, in any status, but not while the patient's record is
/// restricted or anonymised. Every admin but the requester is told. A note has one waiting request at a time (a 409
/// names the one there is). Returns the note, showing the request.
/// </summary>
public sealed record RequestNoteDeletionCommand(Guid PathwayId, Guid NoteId, string Reason, string? Detail, PathwayActor Actor)
    : IRequest<PatientPathwayNoteResponse>;
