using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

/// <summary>
/// Edits a procedure's details; null fields are left as they are. Stages, status and notes change through their own
/// commands.
/// </summary>
public sealed record UpdatePatientProcedureCommand(
    Guid Id,
    string? Title,
    DateOnly? DueDate,
    string? Priority,
    ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
