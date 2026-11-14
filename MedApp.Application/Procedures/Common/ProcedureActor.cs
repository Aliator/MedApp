namespace MedApp.Application.Procedures.Common;

/// <summary>The signed-in staff member making a change, as the audit log records them.</summary>
public sealed record ProcedureActor(Guid UserId, string Username);
