namespace MedApp.Application.Pathways.Common;

/// <summary>
/// The signed-in staff member making a change, as the audit log records them, and whether they're an admin (for the
/// <c>Can...</c> flags of the response they get back; handlers that need the role for a rule take it explicitly).
/// </summary>
public sealed record PathwayActor(Guid UserId, string Username, bool IsAdmin = false);
