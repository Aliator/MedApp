using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;

/// <summary>
/// Replaces the text of a note on a procedure: only its author can, while the procedure's notes can change, and only from
/// the version they saw (<paramref name="ExpectedVersion"/>; otherwise a 409).
/// </summary>
public sealed record EditProcedureNoteCommand(Guid ProcedureId, Guid NoteId, string Text, int ExpectedVersion, ProcedureActor Actor)
    : IRequest<PatientProcedureNoteResponse>;
