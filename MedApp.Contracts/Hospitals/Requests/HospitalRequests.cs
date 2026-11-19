namespace MedApp.Contracts.Hospitals.Requests;

/// <summary>A hospital's details (MC18): its name, ODS code, contact, how its reports go and its default deadline.</summary>
public sealed class HospitalDetailsRequest
{
    public string Name { get; set; } = string.Empty;

    public string OdsCode { get; set; } = string.Empty;

    public string? Contact { get; set; }

    /// <summary>A <c>LetterDispatchMethod</c> by name: <c>Post</c>, <c>HandedToPatient</c>, <c>SecureEmail</c>, <c>EmailOutsideMedApp</c> or <c>Other</c>.</summary>
    public string ReportDeliveryMethod { get; set; } = string.Empty;

    /// <summary>Calendar days for its report when a request names no date (1 to 90).</summary>
    public int DefaultDeadlineDays { get; set; }
}

/// <summary>The patients a hospital request could be for (in a body, so no NHS number is in a URL).</summary>
public sealed class HospitalPatientMatchRequest
{
    public string? NhsNumber { get; set; }

    public string? LastName { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}

/// <summary>A new patient made with a hospital request, when none of the matches is them.</summary>
public sealed class HospitalPatientRequest
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public string? NhsNumber { get; set; }
}

/// <summary>
/// A hospital's request, typed in by the lab (FHIR UK Core <c>ServiceRequest-Lab</c> and <c>Specimen</c>'s shape): the
/// patient chosen from the matches (<see cref="PatientId"/>) or new (<see cref="NewPatient"/>), never both.
/// </summary>
public sealed class CreateHospitalRequestRequest
{
    public Guid HospitalId { get; set; }

    public Guid? PatientId { get; set; }

    public HospitalPatientRequest? NewPatient { get; set; }

    public string HospitalPatientNumber { get; set; } = string.Empty;

    public string? NhsNumber { get; set; }

    public string SurgeonName { get; set; } = string.Empty;

    /// <summary><c>Routine</c>, <c>Urgent</c>, <c>Asap</c> or <c>Stat</c> (FHIR's codes).</summary>
    public string Priority { get; set; } = string.Empty;

    public bool IsCancerPathway { get; set; }

    public DateTime AuthoredOn { get; set; }

    public DateOnly? ReportNeededBy { get; set; }

    public DateTime? CollectedAtUtc { get; set; }

    public Guid SpecimenTypeId { get; set; }

    public Guid LabTestId { get; set; }
}

/// <summary>A page of hospital requests (a POST, as the search may be an NHS number or a case number).</summary>
public sealed class HospitalRequestSearchRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public Guid? HospitalId { get; set; }

    public string? Priority { get; set; }

    public string? Search { get; set; }

    /// <summary><c>case</c>, <c>patient</c>, <c>hospital</c>, <c>needed</c> or <c>registered</c>.</summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;
}
