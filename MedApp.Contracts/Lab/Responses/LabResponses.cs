using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Specimens.Responses;

namespace MedApp.Contracts.Lab.Responses;

/// <summary>A test in the lab's catalogue (MC13).</summary>
public sealed class LabTestResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string? SnomedCode { get; set; }

    public int TargetDays { get; set; }

    public int LimitDays { get; set; }

    public int AtRiskDays { get; set; }

    public List<LabTestStepResponse> Steps { get; set; } = [];

    public bool IsArchived { get; set; }

    /// <summary>Whether it can be deleted: no lab case or sample has used it (else it's archived instead).</summary>
    public bool CanBeDeleted { get; set; }
}

public sealed class LabTestStepResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public bool IsOptional { get; set; }
}

/// <summary>
/// A lab case's strict clock (MC19): the lab's target (received plus the test's days), its limit (shown only), the
/// hospital's date for a hospital case and what set it, which applies (the sooner; a tie names the hospital), when it's
/// at risk and its state. UK dates; date arithmetic only.
/// </summary>
public sealed class LabClockResponse
{
    public DateOnly LabDueOn { get; set; }

    public DateOnly LabLimitOn { get; set; }

    public DateOnly? HospitalDueOn { get; set; }

    /// <summary><c>ReportNeededBy</c>, <c>CancerPathway</c> or <c>HospitalDefault</c>; null for a clinic case.</summary>
    public string? HospitalDueBasis { get; set; }

    public DateOnly DueOn { get; set; }

    /// <summary><c>Lab</c> or <c>Hospital</c>.</summary>
    public string SetBy { get; set; } = string.Empty;

    /// <summary>The end of the due day (UK) in UTC.</summary>
    public DateTime DueAtUtc { get; set; }

    public DateOnly AtRiskOn { get; set; }

    /// <summary><c>OnTime</c>, <c>AtRisk</c>, <c>Breached</c>, <c>ReportedOnTime</c>, <c>ReportedLate</c> or <c>Stopped</c>.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Calendar days from today (UK) to the due date; negative once past.</summary>
    public int DaysLeft { get; set; }
}

/// <summary>A lab case (MC15), with what the viewer may do with it.</summary>
public sealed class LabCaseResponse
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    /// <summary><c>Open</c>, <c>Reported</c>, <c>AwaitingSurgeryDetails</c>, <c>Closed</c>, <c>RepeatRequested</c> or <c>EnteredInError</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary><c>Clinic</c> or <c>Hospital</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public Guid? LabTestId { get; set; }

    public string? LabTestName { get; set; }

    public string? LabTestKind { get; set; }

    public Guid PatientId { get; set; }

    public PathwayPatientResponse? Patient { get; set; }

    public PatientFlagsResponse? PatientFlags { get; set; }

    public Guid? RequestingTeamId { get; set; }

    public string? RequestingTeamName { get; set; }

    public Guid? HospitalId { get; set; }

    public string? HospitalName { get; set; }

    public Guid? HospitalRequestId { get; set; }

    public Guid? PathwayId { get; set; }

    public string? PathwayTitle { get; set; }

    public Guid? StageId { get; set; }

    public string? StageName { get; set; }

    public DateTime OpenedAtUtc { get; set; }

    public string? OpenedByUsername { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    /// <summary>Its clock, once the lab has received it (MC19).</summary>
    public LabClockResponse? Clock { get; set; }

    public List<LabCaseStepResponse> Steps { get; set; } = [];

    /// <summary>What it needs next, or null once it's closed.</summary>
    public LabCaseNextResponse? Next { get; set; }

    public List<SpecimenSummaryResponse> Samples { get; set; } = [];

    /// <summary>The results entered when the report was authorised (free text).</summary>
    public string? ReportText { get; set; }

    public DateTime? ReportedAtUtc { get; set; }

    public string? ReportedByUsername { get; set; }

    /// <summary>The result it made for the requesting clinician (a clinic case).</summary>
    public Guid? ResultId { get; set; }

    public string? RepeatDetail { get; set; }

    public DateTime? RepeatRequestedAtUtc { get; set; }

    public string? RepeatRequestedByUsername { get; set; }

    public string? ErrorDetail { get; set; }

    public DateTime? EnteredInErrorAtUtc { get; set; }

    public string? EnteredInErrorByUsername { get; set; }

    /// <summary>A hospital case's report as it went back (MC20, the next slice); null until then.</summary>
    public LabCaseHospitalReportResponse? HospitalReport { get; set; }

    /// <summary>The hospital's surgery details, as written (MC20, the next slice).</summary>
    public List<LabCaseSurgeryDetailsResponse> SurgeryDetails { get; set; } = [];

    public DateTime? ClosedAtUtc { get; set; }

    public string? ClosedByUsername { get; set; }

    public int Version { get; set; }

    /// <summary>Whether the viewer can record its next step or outcome (the lab team, ProcessSpecimens).</summary>
    public bool CanRecord { get; set; }

    /// <summary>Whether the viewer can enter the results and authorise the report (AuthoriseLabReports), every step done.</summary>
    public bool CanAuthorise { get; set; }

    public bool CanMarkEnteredInError { get; set; }

    public bool CanAddSurgeryDetails { get; set; }

    public string? ReadOnlyReason { get; set; }
}

public sealed class LabCaseStepResponse
{
    public Guid Id { get; set; }

    /// <summary><c>Received</c>, <c>Accessioned</c>, <c>TestStep</c> or <c>Outcome</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid? TestStepId { get; set; }

    public string? StepKind { get; set; }

    public string? Choice { get; set; }

    public string? Outcome { get; set; }

    public string? Detail { get; set; }

    public DateTime AtUtc { get; set; }

    public string? ByUsername { get; set; }

    public bool IsEnteredInError { get; set; }

    public DateTime? EnteredInErrorAtUtc { get; set; }

    public string? EnteredInErrorByUsername { get; set; }

    public string? ErrorDetail { get; set; }

    public bool CanMarkEnteredInError { get; set; }
}

/// <summary>What a case needs next: its sample (<c>Receive</c>), a <c>Step</c>, a <c>Choice</c>'s outcome, or its results (<c>Report</c>).</summary>
public sealed class LabCaseNextResponse
{
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid? TestStepId { get; set; }

    public string? StepKind { get; set; }

    public LabCaseChoiceResponse? Choice { get; set; }
}

/// <summary>A choice and the outcomes its test allows, in a fixed order (none pre-selected).</summary>
public sealed class LabCaseChoiceResponse
{
    public string Choice { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public List<string> Outcomes { get; set; } = [];
}

/// <summary>A lab case in a list.</summary>
public sealed class LabCaseSummaryResponse
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    /// <summary>Where it came from: the clinic's name, or the hospital's.</summary>
    public string? SourceName { get; set; }

    public Guid PatientId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string? PatientNhsNumber { get; set; }

    public string? LabTestName { get; set; }

    public string? NextName { get; set; }

    public string? NextKind { get; set; }

    public DateTime OpenedAtUtc { get; set; }

    public DateTime? ReportedAtUtc { get; set; }

    public DateTime? ReportSentAtUtc { get; set; }

    public LabClockResponse? Clock { get; set; }
}

/// <summary>
/// The home page's counts of the open lab cases the caller's teams see: those whose clock is at risk, breached
/// (overdue), and those with every step done, ready to report.
/// </summary>
public sealed class LabCaseCountResponse
{
    public int AtRisk { get; set; }

    public int Overdue { get; set; }

    public int ToReport { get; set; }
}

/// <summary>A hospital case's report as it went back (MC20; FHIR UK Core <c>DiagnosticReport-Lab</c>'s shape).</summary>
public sealed class LabCaseHospitalReportResponse
{
    public string Status { get; set; } = string.Empty;

    public string LetterKind { get; set; } = string.Empty;

    public Guid HospitalId { get; set; }

    public string? HospitalName { get; set; }

    public string? AddressedTo { get; set; }

    public string? DeliveryMethod { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime SentAtUtc { get; set; }

    public string? SentByUsername { get; set; }
}

/// <summary>The hospital's surgery details, as written (MC20).</summary>
public sealed class LabCaseSurgeryDetailsResponse
{
    public Guid Id { get; set; }

    public DateOnly? SurgeryOn { get; set; }

    public string? Procedure { get; set; }

    public string? Outcome { get; set; }

    public bool NoSurgeryExpected { get; set; }

    public string? Detail { get; set; }

    public DateTime AddedAtUtc { get; set; }

    public string? AddedByUsername { get; set; }
}
