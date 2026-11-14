using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;

/// <summary>A status change made by hand: block, unblock, cancel, complete or archive (see <see cref="ProcedureWorkflow"/>).</summary>
public sealed record ChangePatientProcedureStatusCommand(
    Guid Id,
    string Status,
    string? Reason,
    ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
