using MediatR;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Application.Patients.Queries.GetAllPatients;

public sealed class GetAllPatientsHandler(IPatientRepository repository)
    : IRequestHandler<GetAllPatientsQuery, IReadOnlyList<PatientResponse>>
{
    public async Task<IReadOnlyList<PatientResponse>> Handle(GetAllPatientsQuery request, CancellationToken cancellationToken)
    {
        var patients = await repository.GetAllAsync(cancellationToken);
        return patients.Select(PatientMapper.ToResponse).ToList();
    }
}