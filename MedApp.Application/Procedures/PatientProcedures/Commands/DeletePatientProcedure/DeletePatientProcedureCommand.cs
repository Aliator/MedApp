using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;

public sealed record DeletePatientProcedureCommand(Guid PatientProcedureId) : IRequest<bool>;