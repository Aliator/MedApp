namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// Where a procedure is in its workflow. The numbers are stored in the database, so they never change: 2 was
/// NotStarted, which was dropped (rows moved to Assigned in the AddedProcedureWorkflow migration).
/// </summary>
public enum PatientProcedureStatus
{
    Unassigned = 0,
    Assigned = 1,
    InProgress = 3,
    InReview = 4,
    Completed = 5,
    Cancelled = 6,
    Blocked = 7,
    Archived = 8
}
