using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;

/// <summary>Unassigns a staff member (admins only). Assignment puts the procedure on their task list.</summary>
public sealed record UnassignPatientProcedureCommand(Guid ProcedureId, Guid UserId, ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
