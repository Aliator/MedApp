namespace MedApp.Contracts.Notifications.Requests;

/// <summary>Which notifications the signed-in user wants.</summary>
public sealed class UpdateNotificationPreferencesRequest
{
    /// <summary>Whether to be told when someone assigns them to a procedure.</summary>
    public bool ProcedureAssigned { get; set; } = true;
}
