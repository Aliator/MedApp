using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UnlockProcedureStage;

/// <summary>Reopens a finished stage so it can be corrected and finished again (admins only).</summary>
public sealed record UnlockProcedureStageCommand(Guid ProcedureId, Guid StageId, ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
