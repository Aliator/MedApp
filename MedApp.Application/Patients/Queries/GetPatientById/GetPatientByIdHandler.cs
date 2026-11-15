using MedApp.Application.Patients.Repositories;
using MediatR;
using MedApp.Domain.Patients;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Application.Patients.Queries.GetPatientById;

public sealed class GetPatientByIdHandler(IPatientRepository repository)
    : IRequestHandler<GetPatientByIdQuery, PatientResponse?>
{
    public async Task<PatientResponse?> Handle(GetPatientByIdQuery request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.Id, cancellationToken);
        return patient is null ? null : PatientMapper.ToResponse(patient);
    }
}