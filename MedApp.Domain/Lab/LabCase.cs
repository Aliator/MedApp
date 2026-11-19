using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;

namespace MedApp.Domain.Lab;

/// <summary>
/// The lab's unit of work (MC15 of <c>docs/multi-clinic-plan.md</c>): one request's samples, its recorded steps and one
/// report, for a clinic's samples and (MC18) a hospital's alike. Numbered from the specimens' one sequence (H-73).
/// Changed only through <c>LabCaseChange</c>, which writes its audit entries (<see cref="PatientPathwayAuditAction"/>
/// 50 to 55: IDs, names and outcomes, never the results or a detail typed).
/// <para>
/// Patient data: the results (<see cref="ReportText"/>), why a repeat was asked for, every detail typed on a step and
/// the error details are free text, shown on the case's page only, never on a staff timeline, in a notification or a log.
/// </para>
/// </summary>
public sealed class LabCase
{
    public const int MaxReportTextLength = 4000;
    public const int MaxDetailLength = 500;
    public const int MaxWhereLength = 200;

    public Guid Id { get; init; }

    /// <summary>From the lab's one sequence (a hospital case shares its sample's number). Unique.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Null only while a lab stage's case waits for a sample with a test (MC16): the lab chooses it if none has one.</summary>
    public Guid? LabTestId { get; set; }
    public LabTest? LabTest { get; set; }

    public Guid PatientId { get; init; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The lab's: every case is the lab's work (a clinic's is also seen through its pathway).</summary>
    public Guid TeamId { get; set; }

    public SpecimenSource Source { get; init; }

    /// <summary>The clinic that asked (a clinic case's pathway team); null for the hospital's.</summary>
    public Guid? RequestingTeamId { get; init; }
    public MedApp.Domain.Teams.Team? RequestingTeam { get; set; }

    /// <summary>The hospital and its request (MC18); null for a clinic case.</summary>
    public Guid? HospitalId { get; init; }
    public MedApp.Domain.Hospitals.Hospital? Hospital { get; set; }

    public Guid? HospitalRequestId { get; set; }
    public MedApp.Domain.Hospitals.HospitalRequest? HospitalRequest { get; set; }

    public Guid? PathwayId { get; init; }
    public PatientPathway? Pathway { get; set; }

    /// <summary>
    /// The pathway's lab stage the case belongs to (MC16; one open case per stage), else the stage that took the sample.
    /// </summary>
    public Guid? StageId { get; init; }

    public LabCaseStatus Status { get; set; }

    public DateTime OpenedAtUtc { get; init; }
    public Guid OpenedByUserId { get; init; }

    /// <summary>When the lab received its first sample: its clock starts then (MC19). Null while it waits for one.</summary>
    public DateTime? ReceivedAtUtc { get; set; }

    public List<LabCaseStep> Steps { get; set; } = [];

    /// <summary>The samples in it.</summary>
    public List<Specimen> Specimens { get; set; } = [];

    /// <summary>The results the lab entered when the report was authorised (free text, clinical).</summary>
    public string? ReportText { get; set; }
    public DateTime? ReportedAtUtc { get; set; }
    public Guid? ReportedByUserId { get; set; }

    /// <summary>The result a clinic case's report made, for the requesting clinician to sign off. No foreign key.</summary>
    public Guid? ResultId { get; set; }

    /// <summary>Why the sample was insufficient (free text), when a repeat was asked for.</summary>
    public string? RepeatDetail { get; set; }
    public DateTime? RepeatRequestedAtUtc { get; set; }
    public Guid? RepeatRequestedByUserId { get; set; }

    /// <summary>What was wrong (free text), when the case was marked entered in error.</summary>
    public string? ErrorDetail { get; set; }
    public DateTime? EnteredInErrorAtUtc { get; set; }
    public Guid? EnteredInErrorByUserId { get; set; }

    /// <summary>
    /// A hospital case's report sent back to its hospital (MC20), in the same change as its authorising: when, by whom,
    /// to whom (the request's surgeon, as written: never chosen, H-72) and how (the hospital's delivery method).
    /// Recorded as sent; the <c>LabReport</c> letter's PDF and secure delivery come later.
    /// </summary>
    public DateTime? ReportSentAtUtc { get; set; }
    public Guid? ReportSentByUserId { get; set; }
    public string? ReportAddressedTo { get; set; }
    public MedApp.Domain.Letters.LetterDispatchMethod? ReportDeliveryMethod { get; set; }

    /// <summary>The hospital's surgery details, as the lab typed them (MC20): free text, nothing worked out from them.</summary>
    public List<LabCaseSurgeryDetails> SurgeryDetails { get; set; } = [];

    /// <summary>When the case closed: surgery details (or "no surgery expected") added after its report was sent.</summary>
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }

    public int Version { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// What a hospital sent back after its request (MC20): the surgery's date, the procedure, the outcome and any other
/// details, as written, or "no surgery expected" with why. Free text (clinical), shown on the case's page only, never in
/// the audit log (<c>SurgeryDetailsAdded</c> holds its ID only), a notification or a staff timeline.
/// </summary>
public sealed class LabCaseSurgeryDetails
{
    public const int MaxProcedureLength = 200;
    public const int MaxTextLength = 500;

    public Guid Id { get; init; }

    public Guid LabCaseId { get; init; }

    public DateOnly? SurgeryOn { get; init; }

    public string? Procedure { get; set; }

    public string? Outcome { get; set; }

    public bool NoSurgeryExpected { get; init; }

    public string? Detail { get; set; }

    public DateTime AddedAtUtc { get; init; }

    public Guid AddedByUserId { get; init; }
}

/// <summary>
/// A recorded step of a lab case: Received, Accessioned, one of its test's steps, or a choice's outcome; who and when.
/// Only the latest can be marked entered in error (kept, struck through), by whoever recorded it.
/// </summary>
public sealed class LabCaseStep
{
    public Guid Id { get; init; }

    public Guid LabCaseId { get; init; }

    /// <summary>Its place in the case, from 0, in the order recorded.</summary>
    public int Sequence { get; init; }

    public LabCaseStepKind Kind { get; init; }

    /// <summary>The test's step, for a <see cref="LabCaseStepKind.TestStep"/>.</summary>
    public Guid? TestStepId { get; init; }

    public LabChoice? Choice { get; init; }

    public LabOutcome? Outcome { get; init; }

    /// <summary>Free text: where a second opinion went, or why a sample is insufficient. Never in the audit log.</summary>
    public string? Detail { get; set; }

    public DateTime AtUtc { get; init; }

    public Guid ByUserId { get; init; }

    public DateTime? EnteredInErrorAtUtc { get; set; }
    public Guid? EnteredInErrorByUserId { get; set; }
    public string? ErrorDetail { get; set; }

    public bool IsEnteredInError => EnteredInErrorAtUtc is not null;
}
