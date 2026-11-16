using MedApp.Application.Monitoring.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.DevMcp.DemoData;

/// <summary>Someone read a patient's record (and a procedure's) around a moment: the activity log's views come from these.</summary>
public sealed record Touch(Guid UserId, DateTime AtUtc, Guid PatientId, Guid? ProcedureId);

/// <summary>A monitoring rule as the seed matches it: when it was made, and when (if ever) it was switched off.</summary>
public sealed record RuleState(MonitoringRule Rule, DateTime CreatedUtc, DateTime? DisabledUtc);

/// <summary>
/// What every procedure's replay shares: the clinic (staff, calendars, templates, rules), the random choices, and
/// everything the replays produce, to be saved together.
/// </summary>
public sealed class DemoWorld
{
    public required DemoTimeline Timeline { get; init; }
    public required Random Random { get; init; }
    public required DemoCalendars Calendars { get; init; }
    public required IReadOnlyList<StaffMember> Staff { get; init; }
    /// <summary>The templates, with their maps' stage definitions, as procedures are made from them.</summary>
    public required IReadOnlyDictionary<Pathway, PatientProcedureStageTemplate> Templates { get; init; }

    public IReadOnlyDictionary<Pathway, Guid> TemplateIds => Templates.ToDictionary(t => t.Key, t => t.Value.Id);
    public required IReadOnlyDictionary<string, PatientProcedureStageDefinition> Definitions { get; init; }
    public required IReadOnlyList<RuleState> Rules { get; init; }

    public ScheduleAllocator Allocator => Calendars.Allocator;

    public List<PatientProcedure> Procedures { get; } = [];
    public List<PatientProcedureAuditEntry> Audit { get; } = [];
    public List<Appointment> Appointments { get; } = [];
    public List<AppointmentEvent> AppointmentEvents { get; } = [];
    public List<Notification> Notifications { get; } = [];
    public List<Touch> Touches { get; } = [];

    /// <summary>Procedures to delete (by an admin, later), with when.</summary>
    public List<(PatientProcedure Procedure, DateTime AtUtc)> Deletions { get; } = [];

    /// <summary>Stages finished by an admin without their visit (the audited bypass).</summary>
    public int Bypasses { get; set; }

    public StaffMember Member(string username) => DemoStaff.Get(Staff, username);

    public StaffMember Lead => Staff.Single(s => s.Kind == StaffKind.ClinicalLead);

    public StaffMember Manager => Member(DemoStaff.ManagerUsername);

    public StaffMember Coordinator => Member(DemoStaff.CoordinatorUsername);

    public string UsernameOf(Guid id) => Staff.Single(s => s.Id == id).Username;

    /// <summary>An admin to assign work or archive it at a moment: the lead, or the service manager once they're an admin.</summary>
    public StaffMember AdminAt(DateTime utc) =>
        Manager.IsAdminAt(utc) && Random.NextDouble() < 0.45 ? Manager : Lead;

    /// <summary>Someone's notification choices as they stood at a moment (Account Settings): one person turned assignments off.</summary>
    public NotificationPreferences PreferencesAt(StaffMember member, DateTime utc) =>
        member.Username == DemoStaff.NoAssignmentNotificationsUsername && utc >= Timeline.NotificationsTurnedOffUtc
            ? new NotificationPreferences { UserId = member.Id, ProcedureAssigned = false, UpdatedAtUtc = Timeline.NotificationsTurnedOffUtc }
            : NotificationPreferences.Defaults(member.Id);

    /// <summary>
    /// Whether a notification would be delivered at a moment: as <c>NotificationSender</c> decides, only an optional
    /// kind looks at the person's choices, and those were as <see cref="PreferencesAt"/> says.
    /// </summary>
    public bool Delivers(Notification notification, DateTime utc) =>
        !NotificationPreferences.IsOptional(notification.Kind)
        || PreferencesAt(Staff.Single(s => s.Id == notification.UserId), utc).Wants(notification.Kind);

    /// <summary>The rules switched on at a moment (as the app loads them: <c>GetEnabledAsync</c>), copied as they were then.</summary>
    public IReadOnlyList<MonitoringRule> RulesEnabledAt(DateTime utc) =>
        Rules.Where(r => r.CreatedUtc <= utc && (r.DisabledUtc is null || utc < r.DisabledUtc))
            .Select(r => new MonitoringRule
            {
                Id = r.Rule.Id,
                OwnerUserId = r.Rule.OwnerUserId,
                SubjectUserId = r.Rule.SubjectUserId,
                Event = r.Rule.Event,
                MinimumPriority = r.Rule.MinimumPriority,
                TemplateId = r.Rule.TemplateId,
                StageDefinitionId = r.Rule.StageDefinitionId,
                IsEnabled = true,
                CreatedAtUtc = r.Rule.CreatedAtUtc,
                UpdatedAtUtc = r.Rule.UpdatedAtUtc,
            })
            .ToList();

    /// <summary>Whether a rule's owner was an admin at a moment (rules of someone who isn't are skipped, as the app does).</summary>
    public bool IsAdminAt(Guid userId, DateTime utc) => Staff.Any(s => s.Id == userId && s.IsAdminAt(utc));

    /// <summary>
    /// What the app's monitor would send after a change (<c>ProcedureMonitor</c>): its own matching
    /// (<c>ProcedureMonitor.FindMatchesAsync</c>)
    /// and messages, answering its two questions (the switched-on rules for some events, and which owners are admins)
    /// as they stood at that moment, from memory. Its tasks are already complete, so waiting on them never blocks.
    /// </summary>
    public IEnumerable<Notification> Monitor(PatientProcedure procedure, IReadOnlyList<PatientProcedureAuditEntry> entries, Guid actorUserId, DateTime atUtc)
    {
        var matches = ProcedureMonitor.FindMatchesAsync(
                procedure,
                entries,
                actorUserId,
                (events, _) => Task.FromResult<IReadOnlyList<MonitoringRule>>(RulesEnabledAt(atUtc).Where(r => events.Contains(r.Event)).ToList()),
                (owners, _) => Task.FromResult<IReadOnlySet<Guid>>(owners.Where(o => IsAdminAt(o, atUtc)).ToHashSet()),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        return matches.Select(m => MonitoringNotifications.Create(m.OwnerUserId, m.Activity, procedure, atUtc)).ToList();
    }
}
