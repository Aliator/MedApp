using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetAllPatientProceduresForUser;

public sealed record GetAllPatientProceduresForUserQuery(Guid UserId) : IRequest<IReadOnlyList<PatientProcedureResponse>>;