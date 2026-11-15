namespace MedApp.Domain.Notifications;

/// <summary>What a notification is about. Stored as its number, so never renumber these.</summary>
public enum NotificationKind
{
    /// <summary>Someone assigned the user to a procedure.</summary>
    ProcedureAssigned = 0
}
