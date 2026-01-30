using MediatR;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Queries.GetAllPatients;

public sealed class GetAllPatientsHandler(IPatientRepository repository)
    : IRequestHandler<GetAllPatientsQuery, IEnumerable<Patient>>
{
    public async Task<IEnumerable<Patient>> Handle(GetAllPatientsQuery request, CancellationToken cancellationToken)
    {
        return await repository.GetAllAsync(cancellationToken);
    }
}