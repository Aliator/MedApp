using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;

/// <summary>
/// Finishes and locks a stage, moving the procedure on to its next stage (or to review after the last). A stage whose
/// visit is Required needs an Attended appointment first, unless an admin (<paramref name="IsAdmin"/>) finishes it
/// <paramref name="WithoutVisit"/>, giving a <paramref name="Reason"/>.
/// </summary>
public sealed record FinishProcedureStageCommand(
    Guid ProcedureId,
    Guid StageId,
    ProcedureActor Actor,
    bool WithoutVisit = false,
    string? Reason = null,
    bool IsAdmin = false)
    : IRequest<PatientProcedureResponse>;
