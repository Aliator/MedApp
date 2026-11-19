using MedApp.Contracts.Pathways.PatientPathways.Responses;

namespace MedApp.Contracts.Hospitals.Responses;

/// <summary>A hospital (MC18).</summary>
public sealed class HospitalResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OdsCode { get; set; } = string.Empty;

    public string? Contact { get; set; }

    public string ReportDeliveryMethod { get; set; } = string.Empty;

    public int DefaultDeadlineDays { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>Whether it can be deleted: it has sent no requests (else it's archived).</summary>
    public bool CanBeDeleted { get; set; }
}

/// <summary>A patient a hospital request could be for, for a person to choose from (never chosen for them, H-72).</summary>
public sealed class HospitalPatientMatchResponse
{
    public Guid PatientId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public string? NhsNumber { get; set; }

    public bool IsRestricted { get; set; }
}

/// <summary>A hospital request, with its patient and lab case.</summary>
public sealed class HospitalRequestResponse
{
    public Guid Id { get; set; }

    public Guid HospitalId { get; set; }

    public string? HospitalName { get; set; }

    public string? HospitalOdsCode { get; set; }

    public Guid PatientId { get; set; }

    public PathwayPatientResponse? Patient { get; set; }

    public string HospitalPatientNumber { get; set; } = string.Empty;

    /// <summary>The NHS number as the request gave it.</summary>
    public string? NhsNumber { get; set; }

    /// <summary>Whether the request's NHS number and the record's differ (both shown, H-72).</summary>
    public bool NhsNumbersDiffer { get; set; }

    public string SurgeonName { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public bool IsCancerPathway { get; set; }

    public DateTime AuthoredOn { get; set; }

    public DateOnly? ReportNeededBy { get; set; }

    public DateTime? CollectedAtUtc { get; set; }

    public Guid SpecimenTypeId { get; set; }

    public string? SpecimenTypeName { get; set; }

    public string? SpecimenTypeSnomedCode { get; set; }

    public Guid LabTestId { get; set; }

    public string? LabTestName { get; set; }

    /// <summary>Always <c>Hospital</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public Guid LabCaseId { get; set; }

    public string? LabCaseNumber { get; set; }

    public string? LabCaseStatus { get; set; }

    public DateTime RegisteredAtUtc { get; set; }

    public string? RegisteredByUsername { get; set; }
}

/// <summary>A hospital request in the intake list.</summary>
public sealed class HospitalRequestSummaryResponse
{
    public Guid Id { get; set; }

    public string? LabCaseNumber { get; set; }

    public string? LabCaseStatus { get; set; }

    public string? HospitalName { get; set; }

    public Guid PatientId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string? PatientNhsNumber { get; set; }

    public string HospitalPatientNumber { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public string? LabTestName { get; set; }

    public string Source { get; set; } = string.Empty;

    public bool IsCancerPathway { get; set; }

    public DateOnly? ReportNeededBy { get; set; }

    public DateTime RegisteredAtUtc { get; set; }
}
