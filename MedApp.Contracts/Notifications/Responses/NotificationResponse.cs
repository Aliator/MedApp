namespace MedApp.Contracts.Notifications.Responses;

/// <summary>A notification. <c>Kind</c> is its kind's name, e.g. "ProcedureAssigned".</summary>
public sealed record NotificationResponse(
    Guid Id,
    string Kind,
    string Title,
    string Message,
    Guid? ProcedureId,
    DateTime CreatedAtUtc,
    bool IsRead);
