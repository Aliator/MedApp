using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;

public sealed record GetPatientProcedureByIdQuery(Guid PatientProcedureId) : IRequest<PatientProcedureResponse>;