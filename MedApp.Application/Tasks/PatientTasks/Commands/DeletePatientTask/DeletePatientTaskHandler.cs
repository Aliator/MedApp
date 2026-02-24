using MedApp.Application.Tasks.Repositories;
using MediatR;

namespace MedApp.Application.Tasks.PatientTasks.Commands.DeletePatientTask;

public sealed class DeletePatientTaskHandler(IPatientTaskRepository repository)
    : IRequestHandler<DeletePatientTaskCommand, bool>
{
    public async Task<bool> Handle(DeletePatientTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(request.PatientTaskId, cancellationToken);

        if (task is null)
        {
            return false;
        }

        await repository.DeleteAsync(request.PatientTaskId, cancellationToken);

        return true;
    }
}