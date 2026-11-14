using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;

/// <summary>Starts work on the current stage. Saving notes or finishing a stage also starts it.</summary>
public sealed record StartProcedureStageCommand(Guid ProcedureId, Guid StageId, ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
