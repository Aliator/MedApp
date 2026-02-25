using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Queries.GetPatientTaskById;

public sealed record GetPatientTaskByIdQuery(Guid PatientTaskId) : IRequest<PatientTaskResponse>;