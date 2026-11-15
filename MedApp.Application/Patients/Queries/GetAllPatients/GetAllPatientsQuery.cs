using MedApp.Domain.Patients;
using MediatR;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Application.Patients.Queries.GetAllPatients;

public sealed record GetAllPatientsQuery : IRequest<IReadOnlyList<PatientResponse>>;
