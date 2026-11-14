using MedApp.Application.Procedures.Common;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;

/// <summary>Deletes a Cancelled or Archived procedure (admins only). Its audit history is kept.</summary>
public sealed record DeletePatientProcedureCommand(Guid Id, ProcedureActor Actor) : IRequest;
