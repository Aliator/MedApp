using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetAllPatientProceduresForPatient;

public sealed record GetAllPatientProceduresForPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<PatientProcedureResponse>>;