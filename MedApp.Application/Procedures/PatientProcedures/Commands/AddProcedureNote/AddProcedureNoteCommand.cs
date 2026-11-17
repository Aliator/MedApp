using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;

/// <summary>Adds a note to a procedure, by the actor. Allowed whenever its notes can change (not Cancelled or Archived).</summary>
public sealed record AddProcedureNoteCommand(Guid ProcedureId, string Text, ProcedureActor Actor)
    : IRequest<PatientProcedureNoteResponse>;
