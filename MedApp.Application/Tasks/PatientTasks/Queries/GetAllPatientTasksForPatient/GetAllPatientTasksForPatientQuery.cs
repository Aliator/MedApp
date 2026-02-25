using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Queries.GetAllPatientTasksForPatient;

public sealed record GetAllPatientTasksForPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<PatientTaskResponse>>;