using MediatR;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Contacts;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;
using MedApp.Application.Patients.Common;

namespace MedApp.Application.Patients.Commands.CreatePatient;

/// <summary>
/// Adds a patient. Their NHS number is optional, and stored as its ten digits; another patient's is a 409. Their contact
/// details are optional too: the address's lines, the postcode (normalised), a UK mobile (stored as <c>+447…</c>) and
/// the GP practice they're registered with, which must exist (404) and not be archived (409).
/// </summary>
public sealed class CreatePatientHandler(
    IPatientRepository repository,
    IPracticeRepository practices,
    IActivityLog activityLog)
    : IRequestHandler<CreatePatientCommand, PatientResponse>
{
    public async Task<PatientResponse> Handle(
        CreatePatientCommand request,
        CancellationToken cancellationToken)
    {
        var nhsNumber = NhsNumbers.Normalise(request.NhsNumber);
        if (nhsNumber is not null && await repository.NhsNumberInUseAsync(nhsNumber, null, cancellationToken))
            throw new ConflictException(NhsNumberRules.InUse);

        Practice? practice = null;
        if (request.PracticeId is { } practiceId)
            practice = await PracticeRules.CheckChosenAsync(practices, practiceId, null, cancellationToken);

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Email = request.Email,
            NhsNumber = nhsNumber,
            AddressLines = PostalAddress.Join(request.AddressLines),
            Postcode = UkPostcodes.Normalise(request.Postcode),
            MobilePhone = UkPhoneNumbers.NormaliseMobile(request.MobilePhone),
            PracticeId = practice?.Id,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        await repository.AddAsync(patient, activityLog.Entry(ActivityAction.PatientCreated, patient.Id), cancellationToken);

        return PatientMapper.ToResponse(patient, practice);
    }
}
