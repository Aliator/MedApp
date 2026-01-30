using Domain.Patients;

namespace MedApp.Application.Patients.Repositories;

public interface IPatientRepository
{
    Task AddAsync(Patient patient, CancellationToken cancellationToken);
    Task UpdateAsync(Patient patient, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken);
}