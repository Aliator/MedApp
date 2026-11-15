using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Common;

/// <summary>Patients as the API returns them. Their audit timestamps stay inside the app.</summary>
public static class PatientMapper
{
    public static PatientResponse ToResponse(Patient patient) => new(
        patient.Id,
        patient.FirstName,
        patient.LastName,
        patient.DateOfBirth,
        patient.Email);
}
