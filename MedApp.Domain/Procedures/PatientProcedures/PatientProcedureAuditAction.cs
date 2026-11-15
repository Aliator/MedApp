namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>What an audit entry records. Stored as a number, so values never change.</summary>
public enum PatientProcedureAuditAction
{
    Created = 0,
    DetailsUpdated = 1,
    StatusChanged = 2,
    Assigned = 3,
    Unassigned = 4,
    StageStarted = 5,
    StageNotesUpdated = 6,
    StageFinished = 7,
    StageUnlocked = 8,
    Deleted = 9
}
