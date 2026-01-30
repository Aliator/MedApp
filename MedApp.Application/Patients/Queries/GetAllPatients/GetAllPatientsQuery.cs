using Domain.Patients;
using MediatR;

namespace MedApp.Application.Patients.Queries.GetAllPatients;

public sealed record GetAllPatientsQuery : IRequest<IEnumerable<Patient>>;
