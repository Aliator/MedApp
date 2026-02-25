using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Queries.GetAllPatientTasksForUser;

public sealed record GetAllPatientTasksForUserQuery(Guid UserId) : IRequest<IReadOnlyList<PatientTaskResponse>>;