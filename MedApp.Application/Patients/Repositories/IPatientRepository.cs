using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Repositories;

public interface IPatientRepository
{
    Task AddAsync(Patient patient, CancellationToken cancellationToken);
    Task UpdateAsync(Patient patient, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>One page of patients matching the query's filters, in its sort order, and the total that match.</summary>
    Task<PagedResult<Patient>> SearchAsync(SearchPatientsQuery query, CancellationToken cancellationToken);
}