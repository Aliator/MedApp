using MedApp.Contracts.Patients.Responses;

namespace MedApp.Application.Patients.Common;

/// <summary>A patient on the retention review list, with when and why they're due.</summary>
public static class RetentionReviewMapper
{
    public static RetentionReviewItemResponse ToResponse(RetentionCandidate patient, RetentionDue due) => new()
    {
        PatientId = patient.PatientId,
        FirstName = patient.FirstName,
        LastName = patient.LastName,
        DateOfBirth = patient.DateOfBirth,
        ConclusionOfTreatment = due.ConclusionOfTreatment,
        RetentionClass = due.Class.ToString(),
        DueOn = due.DueOn,
        KeptUntil = patient.KeptUntil,
        KeptReason = patient.KeptReason
    };
}
