using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Data for the insights tests. "Now" is <see cref="PathwaysTestConstants.Now"/>: Monday 15 June 2026, 12:00 UTC
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

    /// <summary>The audit entries for changing a pathway's status, as PathwayChange writes them (its status changes too).</summary>
    public static IReadOnlyList<PatientPathwayAuditEntry> ChangeStatus(PatientPathway pathway, PatientPathwayStatus to, DateTime atUtc)
    {
        var change = new PathwayChange(pathway, PathwaysTestConstants.Staff, atUtc);
        change.ChangeStatus(to, to is PatientPathwayStatus.Blocked or PatientPathwayStatus.Cancelled ? PathwaysTestConstants.Reason : null);
        return change.AuditEntries;
    }

    /// <summary>The audit entry for a pathway's creation.</summary>
    public static PatientPathwayAuditEntry Created(PatientPathway pathway, DateTime atUtc) =>
        PathwayAudit.Entry(pathway, PathwaysTestConstants.Staff, PatientPathwayAuditAction.Created, atUtc);

    /// <summary>The audit entry for finishing a stage at a moment (its own times aren't changed).</summary>
    public static PatientPathwayAuditEntry Finished(PatientPathway pathway, PatientPathwayStage stage, DateTime atUtc) =>
        PathwayAudit.Entry(pathway, PathwaysTestConstants.Staff, PatientPathwayAuditAction.StageFinished, atUtc, stage);

    /// <summary>The audit entry for editing a pathway's details, as UpdatePatientPathwayHandler writes it.</summary>
    public static PatientPathwayAuditEntry DetailsChanged(PatientPathway pathway, DateTime atUtc, Dictionary<string, object> changes) =>
        PathwayAudit.Entry(pathway, PathwaysTestConstants.Staff, PatientPathwayAuditAction.DetailsUpdated, atUtc, details: new { changes });

    /// <summary>The audit entry for moving a pathway's target date (its own target date isn't changed).</summary>
    public static PatientPathwayAuditEntry DueDateChanged(PatientPathway pathway, DateOnly from, DateOnly to, DateTime atUtc) =>
        DetailsChanged(pathway, atUtc, new Dictionary<string, object> { ["dueDate"] = new { from, to } });

    /// <summary>Sets a stage's times: started and (last) finished.</summary>
    public static PatientPathwayStage Times(this PatientPathwayStage stage, DateTime startedUtc, DateTime finishedUtc)
    {
        stage.StartedAtUtc = startedUtc;
        stage.IsCompleted = true;
        stage.IsLocked = true;
        stage.CompletedAtUtc = finishedUtc;
        return stage;
    }

    public static StatusCount Count(PatientPathwayStatus status, int count, bool isPastDue = false) => new(status, isPastDue, count);

    /// <summary>Totals with nothing in them, to set just the parts a test is about.</summary>
    public static InsightsData EmptyData() => new([], [], 0, 0, [], 0, [], [], [], []);
}
