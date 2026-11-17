using MedApp.Application.Procedures.Common;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;

/// <summary>
/// Deletes a note on a procedure, by an admin, with a reason: soft, so it's hidden but kept. Allowed in any status of
/// the procedure, but not while its patient's record is restricted or anonymised. A request to delete it that was
/// waiting is approved by this, and its requester told.
/// </summary>
public sealed record DeleteProcedureNoteCommand(Guid ProcedureId, Guid NoteId, string Reason, ProcedureActor Actor) : IRequest;
