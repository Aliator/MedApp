using MedApp.Application.Patients.Repositories;
using MediatR;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Queries.GetPatientById;

public sealed class GetPatientByIdHandler(IPatientRepository repository)
    : IRequestHandler<GetPatientByIdQuery, Patient?>
{
    public async Task<Patient?> Handle(GetPatientByIdQuery request, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(request.Id, cancellationToken);
    }
}