using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Hospitals.Responses;
using MedApp.Domain.Hospitals;
using MedApp.Domain.Lab;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Hospitals.Common;

/// <summary>Hospitals and hospital requests as the API returns them (the light demo's <c>hospitals.ts</c>).</summary>
public static class HospitalMapper
{
    public static HospitalResponse ToResponse(Hospital hospital, bool inUse) => new()
    {
        Id = hospital.Id,
        Name = hospital.Name,
        OdsCode = hospital.OdsCode,
        Contact = hospital.Contact,
        ReportDeliveryMethod = hospital.ReportDeliveryMethod.ToString(),
        DefaultDeadlineDays = hospital.DefaultDeadlineDays,
        IsArchived = hospital.IsArchived,
        CanBeDeleted = !inUse,
        ExpectedVersion = hospital.Version,
    };

    /// <summary>A patient a request could be for: names, date of birth, NHS number and whether restricted only.</summary>
    public static HospitalPatientMatchResponse ToMatch(Patient patient) => new()
    {
        PatientId = patient.Id,
        FirstName = patient.FirstName,
        LastName = patient.LastName,
        DateOfBirth = patient.DateOfBirth,
        NhsNumber = patient.NhsNumber,
        IsRestricted = PatientRules.IsRestricted(patient),
    };

    /// <summary>
    /// A request, read with its hospital, patient, sample type and test. <paramref name="caseStatus"/> is its lab case's
    /// (Open when it isn't known).
    /// </summary>
    public static HospitalRequestResponse ToResponse(HospitalRequest request, LabCaseStatus? caseStatus, string? registeredByUsername) => new()
    {
        Id = request.Id,
        HospitalId = request.HospitalId,
        HospitalName = request.Hospital?.Name,
        HospitalOdsCode = request.Hospital?.OdsCode,
        PatientId = request.PatientId,
        Patient = PathwayMapper.ToPatient(request.Patient),
        HospitalPatientNumber = request.HospitalPatientNumber,
        NhsNumber = request.NhsNumber,
        NhsNumbersDiffer = request.NhsNumber is not null
                           && request.Patient?.NhsNumber is { } recorded
                           && request.NhsNumber != recorded,
        SurgeonName = request.SurgeonName,
        Priority = request.Priority.ToString(),
        IsCancerPathway = request.IsCancerPathway,
        AuthoredOn = request.AuthoredOnUtc,
        ReportNeededBy = request.ReportNeededBy,
        CollectedAtUtc = request.CollectedAtUtc,
        SpecimenTypeId = request.SpecimenTypeId,
        SpecimenTypeName = request.SpecimenType?.Name,
        SpecimenTypeSnomedCode = request.SpecimenType?.SnomedCode,
        LabTestId = request.LabTestId,
        LabTestName = request.LabTest?.Name,
        Source = nameof(SpecimenSource.Hospital),
        LabCaseId = request.LabCaseId,
        LabCaseNumber = request.LabCaseNumber,
        LabCaseStatus = (caseStatus ?? LabCaseStatus.Open).ToString(),
        RegisteredAtUtc = request.RegisteredAtUtc,
        RegisteredByUsername = registeredByUsername,
    };

    public static HospitalRequestSummaryResponse ToSummary(HospitalRequest request, LabCaseStatus? caseStatus) => new()
    {
        Id = request.Id,
        LabCaseNumber = request.LabCaseNumber,
        LabCaseStatus = (caseStatus ?? LabCaseStatus.Open).ToString(),
        HospitalName = request.Hospital?.Name,
        PatientId = request.PatientId,
        PatientName = request.Patient is null ? string.Empty : PatientMapper.FullName(request.Patient),
        PatientNhsNumber = request.Patient?.NhsNumber,
        HospitalPatientNumber = request.HospitalPatientNumber,
        Priority = request.Priority.ToString(),
        LabTestName = request.LabTest?.Name,
        Source = nameof(SpecimenSource.Hospital),
        IsCancerPathway = request.IsCancerPathway,
        ReportNeededBy = request.ReportNeededBy,
        RegisteredAtUtc = request.RegisteredAtUtc,
    };
}
