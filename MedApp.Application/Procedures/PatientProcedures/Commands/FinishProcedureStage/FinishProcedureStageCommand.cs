using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;

/// <summary>Finishes and locks a stage, moving the procedure on to its next stage (or to review after the last).</summary>
public sealed record FinishProcedureStageCommand(Guid ProcedureId, Guid StageId, ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
