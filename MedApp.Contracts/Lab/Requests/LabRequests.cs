namespace MedApp.Contracts.Lab.Requests;

/// <summary>A lab test to add or change (MC13): its name, kind, SNOMED CT code, clock days and ordered steps.</summary>
public sealed class LabTestRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary><c>Histology</c>, <c>Cytology</c>, <c>Haematology</c>, <c>ClinicalChemistry</c>, <c>Microbiology</c> or <c>Other</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string? SnomedCode { get; set; }

    public int TargetDays { get; set; }

    public int LimitDays { get; set; }

    public int AtRiskDays { get; set; }

    /// <summary>1 to 20 steps, in order. A step kept in its place keeps its ID.</summary>
    public List<LabTestStepRequest> Steps { get; set; } = [];
}

public sealed class LabTestStepRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary><c>Standard</c>, <c>ExtraTests</c>, <c>SentForSecondOpinion</c> or <c>SecondOpinionReturned</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public bool IsOptional { get; set; }
}

/// <summary>Opening a lab case by hand for a received sample with none: the lab chooses the test (never chosen for it).</summary>
public sealed class OpenLabCaseRequest
{
    public Guid SpecimenId { get; set; }

    public Guid LabTestId { get; set; }

    /// <summary>The sample's version, as shown.</summary>
    public int ExpectedVersion { get; set; }
}

/// <summary>A page of lab cases: a view (tab), the source and test filters, a search (in the body, as it may be a case number) and a column.</summary>
public sealed class LabCaseSearchRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary><c>Open</c> (the default), <c>AtRisk</c>, <c>Overdue</c>, <c>ToReport</c>, <c>Reported</c>, <c>AwaitingSurgeryDetails</c>, <c>RepeatRequested</c> or <c>All</c>.</summary>
    public string? View { get; set; }

    /// <summary><c>Clinic</c> or <c>Hospital</c>.</summary>
    public string? Source { get; set; }

    public Guid? LabTestId { get; set; }

    public string? Search { get; set; }

    /// <summary><c>number</c>, <c>patient</c>, <c>source</c>, <c>test</c>, <c>opened</c> or <c>due</c>.</summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;
}

/// <summary>The next step recorded: one of the test's (by its ID), or the fixed <c>Accessioned</c>.</summary>
public sealed class RecordLabStepRequest
{
    public Guid? TestStepId { get; set; }

    /// <summary><c>Accessioned</c>, when it's the next step; else null.</summary>
    public string? FixedStep { get; set; }

    /// <summary>When it was done: now when null, else at most 7 days back and not before the step before.</summary>
    public DateTime? AtUtc { get; set; }

    /// <summary>Where it went, for a second opinion (required then); free text, never in the audit log.</summary>
    public string? Detail { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>A choice's outcome, as the lab member recorded it (never chosen or suggested for them).</summary>
public sealed class RecordLabOutcomeRequest
{
    /// <summary><c>SampleAdequate</c> or <c>NeedsMoreWork</c>: the choice asked now.</summary>
    public string? Choice { get; set; }

    /// <summary><c>Adequate</c>, <c>Insufficient</c>, <c>NoMoreWork</c>, <c>ExtraTests</c> or <c>SecondOpinion</c>.</summary>
    public string? Outcome { get; set; }

    /// <summary>Why the sample is insufficient (required then); free text, never in the audit log.</summary>
    public string? Detail { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>The results entered and the report authorised, in one action (AuthoriseLabReports).</summary>
public sealed class AuthoriseLabReportRequest
{
    /// <summary>The results, as the lab writes them (free text, at most 4,000 characters).</summary>
    public string? Results { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>Marking a step, or the whole case, entered in error: what was wrong.</summary>
public sealed class LabCaseCorrectionRequest
{
    public string? Detail { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>The hospital's surgery details, as written (MC20, the next slice): the date, procedure and outcome, or none expected.</summary>
public sealed class AddSurgeryDetailsRequest
{
    public DateOnly? SurgeryOn { get; set; }

    public string? Procedure { get; set; }

    public string? Outcome { get; set; }

    public bool NoSurgeryExpected { get; set; }

    public string? Detail { get; set; }

    public int ExpectedVersion { get; set; }
}
