using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;

namespace MedApp.Application.Patients.Commands.UpdatePatient;

/// <summary>
/// Edits a patient's details. The activity log records which fields changed, never their values. A restricted or
/// anonymised record can't be edited (409).
/// </summary>
public sealed class UpdatePatientHandler(IPatientRepository repository, IActivityLog activityLog)
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

        patient.LastUpdated = DateTime.UtcNow;

        await repository.UpdateAsync(
            patient,
            activityLog.Entry(ActivityAction.PatientUpdated, patient.Id, new { fields = changed }),
            cancellationToken);

        return PatientMapper.ToResponse(patient);
    }
}
