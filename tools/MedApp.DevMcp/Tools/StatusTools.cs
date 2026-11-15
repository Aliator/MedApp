using System.ComponentModel;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

[McpServerToolType]
public sealed class StatusTools
{
    [McpServerTool(Name = "get_status", ReadOnly = true, Idempotent = true)]
    [Description("Shows which local database medapp-dev is connected to, applied and pending EF migrations, and row counts for users, roles, sessions, patients and procedures. Use it first to see the app's current state.")]
    public static async Task<object> GetStatus(DevGuard guard, MedAppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();

        return new
        {
            server = guard.Server,
            database = guard.Database,
            migrations = new { applied = applied.Count, latestApplied = applied.LastOrDefault(), pending },
            counts = new
            {
                users = await db.Set<ApplicationUser>().CountAsync(ct),
                roles = await db.Roles.CountAsync(ct),
                activeSessions = await db.UserSessions.CountAsync(s => s.RevokedAtUtc == null && s.ExpiresAtUtc > now, ct),
                patients = await db.Patients.CountAsync(ct),
                procedures = await db.PatientProcedures.CountAsync(ct),
                stageDefinitions = await db.PatientProcedureStageDefinitions.CountAsync(ct),
                stageTemplates = await db.PatientProcedureStageTemplates.CountAsync(ct),
            },
        };
    }
}
