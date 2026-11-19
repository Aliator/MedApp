namespace MedApp.Domain.Lab;

// The lab's stored enums (MC13 and MC15 of docs/multi-clinic-plan.md, section 7). Stored by number: never renumber,
// add new values with new numbers.

/// <summary>What kind of test a catalogue entry is.</summary>
public enum LabTestKind
{
    Histology = 0,
    Cytology = 1,
    Haematology = 2,
    ClinicalChemistry = 3,
    Microbiology = 4,
    Other = 5
}

/// <summary>A test step's kind: a standard one, or one of the optional kinds "Needs more work?" adds.</summary>
public enum LabStepKind
{
    Standard = 0,
    ExtraTests = 1,
    SentForSecondOpinion = 2,
    SecondOpinionReturned = 3
}

/// <summary>A lab case's status (MC15; the hospital's statuses are used from MC20).</summary>
public enum LabCaseStatus
{
    Open = 0,
    Reported = 1,
    AwaitingSurgeryDetails = 2,
    Closed = 3,
    RepeatRequested = 4,
    EnteredInError = 5
}

/// <summary>A recorded step's kind: the fixed ones, one of the test's steps, or a choice's outcome.</summary>
public enum LabCaseStepKind
{
    Received = 0,
    Accessioned = 1,
    TestStep = 2,
    Outcome = 3
}

/// <summary>The choice points in the lab's own workflow (decision 191: the lab member records each outcome).</summary>
public enum LabChoice
{
    SampleAdequate = 0,
    NeedsMoreWork = 1
}

/// <summary>A choice's outcomes, in the fixed order they're offered (never chosen or suggested for anyone).</summary>
public enum LabOutcome
{
    Adequate = 0,
    Insufficient = 1,
    NoMoreWork = 2,
    ExtraTests = 3,
    SecondOpinion = 4
}
