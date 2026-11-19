using MedApp.Domain.Hospitals;
using MedApp.Domain.Lab;
using MedApp.Domain.Letters;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic hospitals, requests and hospital cases (MC18, MC20): invented names, 999-range NHS numbers.</summary>
public static class HospitalsTestConstants
{
    public const string HospitalName = "Riverside General (test)";
    public const string SurgeonName = "Mr Synthetic Surgeon";
    public const string HospitalPatientNumber = "RGH-TEST-001";

    /// <summary>A synthetic NHS number from the 999 range, with a valid check digit.</summary>
    public const string RequestNhsNumber = "9990000018";

    public static Hospital Hospital(int defaultDeadlineDays = 10) => new()
    {
        Id = Guid.NewGuid(),
        Name = HospitalName,
        OdsCode = "RXX01",
        Contact = "Pathology office (test)",
        ReportDeliveryMethod = LetterDispatchMethod.SecureEmail,
        DefaultDeadlineDays = defaultDeadlineDays,
        Version = 1,
        CreatedAt = PathwaysTestConstants.Now.AddDays(-30),
        LastUpdated = PathwaysTestConstants.Now.AddDays(-30),
    };

    public static HospitalRequest Request(
        Hospital hospital, Patient patient, LabTest test, SpecimenType type, DateOnly? reportNeededBy = null, bool isCancerPathway = false,
        LabRequestPriority priority = LabRequestPriority.Urgent, DateTime? registeredAtUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        HospitalId = hospital.Id,
        Hospital = hospital,
        PatientId = patient.Id,
        Patient = patient,
        TeamId = MedApp.Domain.Teams.Team.LabId,
        HospitalPatientNumber = HospitalPatientNumber,
        NhsNumber = RequestNhsNumber,
        SurgeonName = SurgeonName,
        Priority = priority,
        IsCancerPathway = isCancerPathway,
        AuthoredOnUtc = PathwaysTestConstants.Now.AddDays(-3),
        ReportNeededBy = reportNeededBy,
        CollectedAtUtc = PathwaysTestConstants.Now.AddDays(-3),
        SpecimenTypeId = type.Id,
        SpecimenType = type,
        LabTestId = test.Id,
        LabTest = test,
        LabCaseNumber = "MA26-000700",
        RegisteredAtUtc = registeredAtUtc ?? PathwaysTestConstants.Now.AddDays(-2),
        RegisteredByUserId = Guid.NewGuid(),
    };

    /// <summary>The request's lab case and its received sample (no pathway: the lab's own).</summary>
    public static LabCase Case(HospitalRequest request, LabCaseStatus status = LabCaseStatus.Open)
    {
        var labCase = new LabCase
        {
            Id = Guid.NewGuid(),
            Number = request.LabCaseNumber,
            LabTestId = request.LabTestId,
            LabTest = request.LabTest,
            PatientId = request.PatientId,
            Patient = request.Patient,
            TeamId = MedApp.Domain.Teams.Team.LabId,
            Source = SpecimenSource.Hospital,
            HospitalId = request.HospitalId,
            Hospital = request.Hospital,
            HospitalRequestId = request.Id,
            HospitalRequest = request,
            Status = status,
            OpenedAtUtc = request.RegisteredAtUtc,
            OpenedByUserId = request.RegisteredByUserId,
            ReceivedAtUtc = request.RegisteredAtUtc,
            Version = 2,
        };
        request.LabCaseId = labCase.Id;
        var sample = new Specimen
        {
            Id = Guid.NewGuid(),
            Number = request.LabCaseNumber,
            PatientId = request.PatientId,
            Patient = request.Patient,
            TeamId = MedApp.Domain.Teams.Team.LabId,
            Source = SpecimenSource.Hospital,
            HospitalId = request.HospitalId,
            Hospital = request.Hospital,
            HospitalRequestId = request.Id,
            HospitalRequest = request,
            LabTestId = request.LabTestId,
            LabTest = request.LabTest,
            LabCaseId = labCase.Id,
            LabCase = labCase,
            SpecimenTypeId = request.SpecimenTypeId,
            SpecimenType = request.SpecimenType,
            RequestingUserId = request.RegisteredByUserId,
            Status = SpecimenStatus.Received,
            TakenAtUtc = request.CollectedAtUtc ?? request.RegisteredAtUtc,
            TakenByUserId = request.RegisteredByUserId,
            RecordedAtUtc = request.RegisteredAtUtc,
            ReceivedAtUtc = request.RegisteredAtUtc,
            ReceivedByUserId = request.RegisteredByUserId,
            Version = 1,
        };
        labCase.Specimens.Add(sample);
        return labCase;
    }
}
