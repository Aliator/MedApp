namespace MedApp.Domain.Tasks.PatientTask;

public enum PatientTaskStatus
{
    Unassigned = 0,
    NotStarted = 1,
    InProgress = 2,
    InReview = 3,
    Completed = 4,
    Cancelled = 5,
    Blocked = 6,
    Archived = 7
}