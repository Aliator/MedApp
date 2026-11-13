using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetAllPatientProcedures;

public sealed record GetAllPatientProceduresQuery : IRequest<IReadOnlyList<PatientProcedureResponse>>;