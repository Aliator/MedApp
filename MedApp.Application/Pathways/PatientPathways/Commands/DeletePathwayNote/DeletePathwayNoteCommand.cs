using MedApp.Application.Pathways.Common;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;

/// <summary>
/// Deletes a note on a pathway, by an admin, with a reason (a <c>NoteDeletionReason</c>, by name) and an optional
/// detail: soft, so it's hidden but kept. <paramref name="ExpectedVersion"/> is the note's version the admin saw: a 409 if
/// it was edited since. Allowed in any status of the pathway, but not while its patient's record is restricted or
/// anonymised. A request to delete it that was waiting is approved by this, and its requester told; its author is told too.
/// </summary>
public sealed record DeletePathwayNoteCommand(
    Guid PathwayId, Guid NoteId, string Reason, string? Detail, int ExpectedVersion, PathwayActor Actor) : IRequest;
