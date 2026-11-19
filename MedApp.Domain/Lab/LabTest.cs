namespace MedApp.Domain.Lab;

/// <summary>
/// A test in the lab's catalogue (MC13 of <c>docs/multi-clinic-plan.md</c>): its kind, its SNOMED CT procedure code, its
/// time limits in calendar days (the strict clock's target, its limit, and how long before the due date it's at risk)
/// and its ordered steps. Organisation-wide setup (ManageSetup); one a lab case used is archived, not deleted. Its name
/// and its steps' names are admin-written, so notifications and the audit log may quote them. No patient data.
/// </summary>
public sealed class LabTest
{
    public const int MaxNameLength = 100;
    public const int MaxSnomedCodeLength = 18;
    public const int MinDays = 1;
    public const int MaxDays = 90;
    public const int MaxAtRiskDays = 30;
    public const int MaxSteps = 20;

    /// <summary>The RCPath histopathology benchmark (80% in 7 calendar days, 90% in 10), for a test that names none.</summary>
    public const int DefaultTargetDays = 7;
    public const int DefaultLimitDays = 10;
    public const int DefaultAtRiskDays = 2;

    public Guid Id { get; init; }

    /// <summary>Unique, ignoring case.</summary>
    public string Name { get; set; } = string.Empty;

    public LabTestKind Kind { get; set; }

    /// <summary>Its SNOMED CT procedure code, optional: 6 to 18 digits, never looked up.</summary>
    public string? SnomedCode { get; set; }

    /// <summary>The lab's target, in calendar days from receipt (1 to 90).</summary>
    public int TargetDays { get; set; } = DefaultTargetDays;

    /// <summary>The limit, shown beside the target (the target to 90).</summary>
    public int LimitDays { get; set; } = DefaultLimitDays;

    /// <summary>How many days before the due date a case is at risk (0 to 30, fewer than the target).</summary>
    public int AtRiskDays { get; set; } = DefaultAtRiskDays;

    /// <summary>Its steps, in order (1 to 20).</summary>
    public List<LabTestStep> Steps { get; set; } = [];

    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastUpdated { get; set; }
}

/// <summary>One of a test's ordered steps: its name, its kind, and whether it's done only when "Needs more work?" asks for it.</summary>
public sealed class LabTestStep
{
    public const int MaxNameLength = 100;

    public Guid Id { get; init; }

    public Guid LabTestId { get; init; }

    /// <summary>Its place in the test, from 0.</summary>
    public int Position { get; set; }

    public string Name { get; set; } = string.Empty;

    public LabStepKind Kind { get; set; }

    public bool IsOptional { get; set; }
}
