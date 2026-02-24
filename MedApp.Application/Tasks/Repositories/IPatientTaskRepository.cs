using MedApp.Domain.Tasks.PatientTasks;

namespace MedApp.Application.Tasks.Repositories;

public interface IPatientTaskRepository
{
    Task AddAsync(PatientTask task, CancellationToken ct);
    Task<PatientTask?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<PatientTask>> GetAllAsync(CancellationToken ct);
    Task UpdateAsync(PatientTask task, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}