using MedApp.Domain.Lab;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;

namespace MedApp.Domain.Hospitals;

/// <summary>FHIR's <c>ServiceRequest.priority</c> codes (MC18). Stored by number: never renumber.</summary>
public enum LabRequestPriority
{
    Routine = 0,
    Urgent = 1,
    Asap = 2,
    Stat = 3
}

/// <summary>
/// A hospital's request for the lab (MC18 of <c>docs/multi-clinic-plan.md</c>), typed in by the lab, shaped on FHIR UK
/// Core <c>ServiceRequest-Lab</c> and <c>Specimen</c>. Registering it brings its sample into the samples database and
/// opens its lab case (no pathway: the lab's own). The patient is chosen by a person from the matches, or made with it
/// (never matched automatically, H-72).
/// <para>
/// Patient data: the hospital's patient number, the NHS number as the request gave it, and the surgeon's name as
/// written. Hospital cases are seen by the lab team and admins only.
/// </para>
/// </summary>
public sealed class HospitalRequest
{
    public const int MaxHospitalPatientNumberLength = 30;
    public const int MaxSurgeonNameLength = 200;

    public Guid Id { get; init; }

    public Guid HospitalId { get; init; }
    public Hospital Hospital { get; set; } = null!;

    public Guid PatientId { get; init; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The lab team's: hospital cases are the lab's (decision 200).</summary>
    public Guid TeamId { get; init; }

    /// <summary>The hospital's own number for the patient, as the request gave it.</summary>
    public string HospitalPatientNumber { get; set; } = string.Empty;

    /// <summary>The NHS number as the request gave it (ten digits), if any: the record's may differ, and both are shown.</summary>
    public string? NhsNumber { get; set; }

    /// <summary>The requesting surgeon, as written on the request: the report is addressed to them (MC20).</summary>
    public string SurgeonName { get; set; } = string.Empty;

    public LabRequestPriority Priority { get; init; }

    public bool IsCancerPathway { get; init; }

    /// <summary>When the hospital made the request (FHIR's <c>authoredOn</c>).</summary>
    public DateTime AuthoredOnUtc { get; init; }

    /// <summary>FHIR's <c>occurrence</c>: the UK date the hospital needs the report by, if it gave one (MC19's clock).</summary>
    public DateOnly? ReportNeededBy { get; init; }

    /// <summary>When the sample was taken, if the request says.</summary>
    public DateTime? CollectedAtUtc { get; init; }

    public Guid SpecimenTypeId { get; init; }
    public SpecimenType SpecimenType { get; set; } = null!;

    /// <summary>The test asked for, from the lab's catalogue (MC13).</summary>
    public Guid LabTestId { get; init; }
    public LabTest LabTest { get; set; } = null!;

    /// <summary>
    /// Its lab case: the one it opened, or the one the lab opened by hand for its sample after that was entered in
    /// error. No foreign key (the case points back to its request).
    /// </summary>
    public Guid LabCaseId { get; set; }

    /// <summary>Its lab case's number (the sample's), kept with the pointer for the intake list's search and order.</summary>
    public string LabCaseNumber { get; set; } = string.Empty;

    public DateTime RegisteredAtUtc { get; init; }

    public Guid RegisteredByUserId { get; init; }
}
