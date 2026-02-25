using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Queries.GetAllPatientTasks;

public sealed record GetAllPatientTasksQuery : IRequest<IReadOnlyList<PatientTaskResponse>>;