using MediatR;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Contacts;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

/// <summary>
/// Edits a patient's details. The activity log records which fields changed, never their values. A restricted or
/// anonymised record can't be edited (409). The NHS number is left as it is when not given, removed when given empty,
/// and a 409 when another patient has it. The contact details follow the same rule (null leaves one as it is, empty
/// removes it; the practice is removed with <see cref="Guid.Empty"/>), and a newly chosen practice must exist (404) and
/// not be archived (409); one that's archived since it was chosen stays. A practice that isn't in the directory
/// (<see cref="UpdatePatientCommand.UnlistedPractice"/>) replaces a directory one, and a directory practice (or removing
/// it) replaces an unlisted one: a patient never has both. The activity log names it <c>unlistedPractice</c>.
/// </summary>
public sealed class UpdatePatientHandler(
    IPatientRepository repository,
    IPracticeRepository practices,
    IActivityLog activityLog)
    : IRequestHandler<UpdatePatientCommand, PatientResponse?>
{
    public async Task<PatientResponse?> Handle(
        UpdatePatientCommand request,
        CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.Id, cancellationToken);

        if (patient is null)
            return null;

        PatientRules.EnsureCanChange(patient);

        var changed = new List<string>();

        if (request.FirstName is not null && request.FirstName != patient.FirstName)
        {
            patient.FirstName = request.FirstName;
            changed.Add("firstName");
        }

        if (request.LastName is not null && request.LastName != patient.LastName)
        {
            patient.LastName = request.LastName;
            changed.Add("lastName");
        }

        if (request.DateOfBirth is { } dateOfBirth && dateOfBirth != patient.DateOfBirth)
        {
            patient.DateOfBirth = dateOfBirth;
            changed.Add("dateOfBirth");
        }

        if (request.Email is not null && request.Email != patient.Email)
        {
            patient.Email = request.Email;
            changed.Add("email");
        }

        if (request.NhsNumber is not null)
        {
            var nhsNumber = NhsNumbers.Normalise(request.NhsNumber);
            if (nhsNumber != patient.NhsNumber)
            {
                if (nhsNumber is not null && await repository.NhsNumberInUseAsync(nhsNumber, patient.Id, cancellationToken))
                    throw new ConflictException(NhsNumberRules.InUse);

                patient.NhsNumber = nhsNumber;
                changed.Add("nhsNumber");
            }
        }

        if (request.AddressLines is not null)
        {
            var lines = PostalAddress.Join(request.AddressLines);
            if (lines != patient.AddressLines)
            {
                patient.AddressLines = lines;
                changed.Add("addressLines");
            }
        }

        if (request.Postcode is not null)
        {
            var postcode = UkPostcodes.Normalise(request.Postcode);
            if (postcode != patient.Postcode)
            {
                patient.Postcode = postcode;
                changed.Add("postcode");
            }
        }

        if (request.MobilePhone is not null)
        {
            var mobile = UkPhoneNumbers.NormaliseMobile(request.MobilePhone);
            if (mobile != patient.MobilePhone)
            {
                patient.MobilePhone = mobile;
                changed.Add("mobilePhone");
            }
        }

        if (request.ReminderChannel is not null)
        {
            var channel = ReminderChannelRules.ParseOrNone(request.ReminderChannel);
            if (channel != patient.ReminderChannel)
            {
                patient.ReminderChannel = channel;
                changed.Add("reminderChannel");
            }
        }

        Practice? practice = null;
        if (request.PracticeId is { } practiceId)
        {
            Guid? chosen = practiceId == Guid.Empty ? null : practiceId;
            if (chosen is { } id)
                practice = await PracticeRules.CheckChosenAsync(practices, id, patient.PracticeId, cancellationToken);
            if (chosen != patient.PracticeId)
            {
                patient.PracticeId = chosen;
                changed.Add("practice");
            }

            // A directory practice, or none, replaces an unlisted one (unless one is given with it, below).
            if (request.UnlistedPractice is null && UnlistedPractices.Clear(patient))
                changed.Add("unlistedPractice");
        }

        if (request.UnlistedPractice is { } unlisted)
        {
            // An unlisted practice replaces a directory one: a patient has one or the other.
            if (patient.PracticeId is not null)
            {
                patient.PracticeId = null;
                practice = null;
                if (!changed.Contains("practice"))
                    changed.Add("practice");
            }

            if (UnlistedPractices.Set(patient, unlisted) && !changed.Contains("unlistedPractice"))
                changed.Add("unlistedPractice");
        }

        practice ??= patient.PracticeId is { } current ? await practices.GetAsync(current, cancellationToken) : null;

        patient.LastUpdated = DateTime.UtcNow;

        await repository.UpdateAsync(
            patient,
            activityLog.Entry(ActivityAction.PatientUpdated, patient.Id, new { fields = changed }),
            cancellationToken);

        return PatientMapper.ToResponse(patient, practice);
    }
}
