using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Repositories;

public interface IPatientProcedureRepository
{
    Task AddAsync(PatientProcedure procedure, CancellationToken ct);
    Task<PatientProcedure?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<PatientProcedure>> GetAllAsync(CancellationToken ct);
    Task<IEnumerable<PatientProcedure>> GetAllForUserAsync(Guid userId, CancellationToken ct);
    Task<IEnumerable<PatientProcedure>> GetAllForPatientAsync(Guid patientId, CancellationToken ct);
    Task UpdateAsync(PatientProcedure procedure, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}