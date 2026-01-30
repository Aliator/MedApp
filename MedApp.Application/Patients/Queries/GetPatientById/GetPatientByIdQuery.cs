using MedApp.Domain.Patients;
using MediatR;

namespace MedApp.Application.Patients.Queries.GetPatientById;

public sealed record GetPatientByIdQuery(Guid Id) : IRequest<Patient?>;