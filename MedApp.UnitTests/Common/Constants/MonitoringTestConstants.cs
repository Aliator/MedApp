using MedApp.Domain.Monitoring;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic monitoring rules and the admins who own them. The staff they watch are in PathwaysTestConstants.</summary>
public static class MonitoringTestConstants
{
    /// <summary>An admin who owns rules (not the one making changes in the pathway tests).</summary>
    public static readonly Guid OwnerId = Guid.Parse("0000000c-0000-0000-0000-000000000001");

    /// <summary>A second admin with rules of their own.</summary>
    public static readonly Guid OtherOwnerId = Guid.Parse("0000000c-0000-0000-0000-000000000002");

    public static readonly Guid RuleId = Guid.Parse("0000000c-0000-0000-0000-000000000003");
    public static readonly Guid TemplateId = Guid.Parse("0000000c-0000-0000-0000-000000000004");

    public static readonly DateTime CreatedAtUtc = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    public const string BloodsStage = "Bloods";

    public static readonly Guid RuleTeamId = Guid.Parse("0000000c-0000-0000-0000-000000000005");

    public const string RuleTeamName = "Dermatology";

    /// <summary>A switched-on rule, with no conditions unless given.</summary>
    public static MonitoringRule CreateRule(
        MonitoringEvent monitoringEvent,
        Guid? ownerId = null,
        Guid? subjectUserId = null,
        PatientPathwayPriority? minimumPriority = null,
        Guid? templateId = null,
        Guid? stageDefinitionId = null,
        bool isEnabled = true,
        Guid? id = null,
        int? thresholdDays = null,
        Guid? teamId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        OwnerUserId = ownerId ?? OwnerId,
        SubjectUserId = subjectUserId,
        Event = monitoringEvent,
        MinimumPriority = minimumPriority,
        TemplateId = templateId,
        StageDefinitionId = stageDefinitionId,
        ThresholdDays = thresholdDays,
        TeamId = teamId,
        IsEnabled = isEnabled,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = CreatedAtUtc,
    };
}
