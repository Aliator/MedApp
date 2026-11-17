using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

/// <summary>
/// Edits a procedure's details; null fields are left as they are. Stages, status and notes change through their own
/// commands.
/// <para>
/// <paramref name="Notes"/> is only there to refuse it (a 400): a page loaded before notes became a list still sends the
/// old single field, and ignoring it would drop the change without a word. Remove it a release after it's no longer
/// sent (<c>docs/todo.md</c>).
/// </para>
/// </summary>
public sealed record UpdatePatientProcedureCommand(
    Guid Id,
    string? Title,
    DateOnly? DueDate,
    string? Priority,
    ProcedureActor Actor,
    string? Notes = null)
    : IRequest<PatientProcedureResponse>;
