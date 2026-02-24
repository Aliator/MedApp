using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.DeletePatientTask;

public sealed record DeletePatientTaskCommand(Guid PatientTaskId) : IRequest<bool>;