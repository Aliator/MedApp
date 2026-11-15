using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;

/// <summary>Assigns a staff member (admins only). Assignment puts the procedure on their task list.</summary>
public sealed record AssignPatientProcedureCommand(Guid ProcedureId, Guid UserId, ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
