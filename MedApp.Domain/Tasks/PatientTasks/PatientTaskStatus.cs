namespace MedApp.Domain.Tasks.PatientTasks;

public enum PatientTaskStatus
{
    Unassigned = 0,
    Assigned = 1,
    NotStarted = 2,
    InProgress = 3,
    InReview = 4,
    Completed = 5,
    Cancelled = 6,
    Blocked = 7,
    Archived = 8
}