using MediatR;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.Application.Patients.Common;

namespace MedApp.Application.Patients.Commands.CreatePatient;

/// <summary>Adds a patient. Their NHS number is optional, and stored as its ten digits; another patient's is a 409.</summary>
public sealed class CreatePatientHandler(IPatientRepository repository, IActivityLog activityLog)
    : IRequestHandler<CreatePatientCommand, PatientResponse>
{
    public async Task<PatientResponse> Handle(
        CreatePatientCommand request,
        CancellationToken cancellationToken)
    {
        var nhsNumber = NhsNumbers.Normalise(request.NhsNumber);
        if (nhsNumber is not null && await repository.NhsNumberInUseAsync(nhsNumber, null, cancellationToken))
            throw new ConflictException(NhsNumberRules.InUse);

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Email = request.Email,
            NhsNumber = nhsNumber,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        await repository.AddAsync(patient, activityLog.Entry(ActivityAction.PatientCreated, patient.Id), cancellationToken);

        return PatientMapper.ToResponse(patient);
    }
}
