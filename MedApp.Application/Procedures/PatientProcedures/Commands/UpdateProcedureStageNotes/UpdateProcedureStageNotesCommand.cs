using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;

/// <summary>Replaces a stage's notes: the current stage, or a finished one an admin has unlocked.</summary>
public sealed record UpdateProcedureStageNotesCommand(Guid ProcedureId, Guid StageId, string Notes, ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
