using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Insights.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Data for the insights tests. "Now" is <see cref="ProceduresTestConstants.Now"/>: Monday 15 June 2026, 12:00 UTC
/// (13:00 BST), so this UK week began at Sunday 14 June 23:00 UTC.
/// </summary>
public static class InsightsTestConstants
{
    public static readonly DateOnly ThisMonday = new(2026, 6, 15);

    /// <summary>Monday 00:00 BST, as UTC.</summary>
    public static readonly DateTime ThisWeekStartUtc = new(2026, 6, 14, 23, 0, 0, DateTimeKind.Utc);

    public static readonly DateTime LastWeekStartUtc = ThisWeekStartUtc.AddDays(-7);

    public static readonly Guid TemplateId = Guid.Parse("0000000e-0000-0000-0000-000000000001");
    public static readonly Guid OtherTemplateId = Guid.Parse("0000000e-0000-0000-0000-000000000002");
    public const string OtherTemplateName = "Other template";

    /// <summary>The audit entries for changing a procedure's status, as ProcedureChange writes them (its status changes too).</summary>
    public static IReadOnlyList<PatientProcedureAuditEntry> ChangeStatus(PatientProcedure procedure, PatientProcedureStatus to, DateTime atUtc)
    {
        var change = new ProcedureChange(procedure, ProceduresTestConstants.Staff, atUtc);
        change.ChangeStatus(to, to is PatientProcedureStatus.Blocked or PatientProcedureStatus.Cancelled ? ProceduresTestConstants.Reason : null);
        return change.AuditEntries;
    }

    /// <summary>The audit entry for a procedure's creation.</summary>
    public static PatientProcedureAuditEntry Created(PatientProcedure procedure, DateTime atUtc) =>
        ProcedureAudit.Entry(procedure, ProceduresTestConstants.Staff, PatientProcedureAuditAction.Created, atUtc);

    /// <summary>The audit entry for finishing a stage at a moment (its own times aren't changed).</summary>
    public static PatientProcedureAuditEntry Finished(PatientProcedure procedure, PatientProcedureStage stage, DateTime atUtc) =>
        ProcedureAudit.Entry(procedure, ProceduresTestConstants.Staff, PatientProcedureAuditAction.StageFinished, atUtc, stage);

    /// <summary>Sets a stage's times: started and (last) finished.</summary>
    public static PatientProcedureStage Times(this PatientProcedureStage stage, DateTime startedUtc, DateTime finishedUtc)
    {
        stage.StartedAtUtc = startedUtc;
        stage.IsCompleted = true;
        stage.IsLocked = true;
        stage.CompletedAtUtc = finishedUtc;
        return stage;
    }

    public static StatusCount Count(PatientProcedureStatus status, int count, bool isPastDue = false) => new(status, isPastDue, count);

    /// <summary>Totals with nothing in them, to set just the parts a test is about.</summary>
    public static InsightsData EmptyData() => new([], [], 0, 0, [], 0, [], [], [], []);
}
