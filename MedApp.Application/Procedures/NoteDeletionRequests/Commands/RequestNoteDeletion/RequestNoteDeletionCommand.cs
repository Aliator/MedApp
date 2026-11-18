using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;

/// <summary>
/// Asks an admin to delete a note on a procedure, with a reason (a <c>NoteDeletionReason</c>, by name) and an optional
/// detail: any note on a procedure the member of staff can see, in any status, but not while the patient's record is
/// restricted or anonymised. Every admin but the requester is told. A note has one waiting request at a time (a 409
/// names the one there is). Returns the note, showing the request.
/// </summary>
public sealed record RequestNoteDeletionCommand(Guid ProcedureId, Guid NoteId, string Reason, string? Detail, ProcedureActor Actor)
    : IRequest<PatientProcedureNoteResponse>;
