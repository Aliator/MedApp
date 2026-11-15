using MedApp.Domain.Patients;
using MediatR;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Application.Patients.Queries.GetPatientById;

public sealed record GetPatientByIdQuery(Guid Id) : IRequest<PatientResponse?>;