using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Attachments;
using MedApp.Domain.Calendar;
using MedApp.Domain.Consent;
using MedApp.Domain.Mdt;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Teams;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Someone read a patient's record (and a pathway's, or a referral's) around a moment: the activity log's views come
/// from these.
/// </summary>
public sealed record Touch(Guid UserId, DateTime AtUtc, Guid PatientId, Guid? PathwayId, Guid? ReferralId = null);

/// <summary>A monitoring rule as the seed matches it: when it was made, and when (if ever) it was switched off.</summary>
public sealed record RuleState(MonitoringRule Rule, DateTime CreatedUtc, DateTime? DisabledUtc);

/// <summary>
/// What every pathway's replay shares: the centre (staff, calendars, templates, rules), the random choices, and
/// everything the replays produce, to be saved together.
/// </summary>
public sealed class DemoWorld
{
    public required DemoTimeline Timeline { get; init; }
    public required Random Random { get; init; }
    public required DemoCalendars Calendars { get; init; }
    public required IReadOnlyList<StaffMember> Staff { get; init; }
    /// <summary>The templates, with their maps' stage definitions, as pathways are made from them.</summary>
    public required IReadOnlyDictionary<PathwayKind, PathwayTemplate> Templates { get; init; }

    public IReadOnlyDictionary<PathwayKind, Guid> TemplateIds => Templates.ToDictionary(t => t.Key, t => t.Value.Id);
    public required IReadOnlyDictionary<string, PatientPathwayStageDefinition> Definitions { get; init; }
    public required IReadOnlyList<RuleState> Rules { get; init; }

    /// <summary>The teams as stored (with their waiting-time targets), by ID.</summary>
    public required IReadOnlyDictionary<Guid, Team> Teams { get; init; }

    public Team Team(string name) => Teams.Values.Single(s => s.Name == name);

    public ScheduleAllocator Allocator => Calendars.Allocator;

    public List<PatientPathway> Pathways { get; } = [];
    public List<PatientPathwayAuditEntry> Audit { get; } = [];

    /// <summary>Every version of every pathway note (the notes themselves are on their pathways).</summary>
    public List<PatientPathwayNoteVersion> NoteVersions { get; } = [];
    public List<Appointment> Appointments { get; } = [];
    public List<AppointmentEvent> AppointmentEvents { get; } = [];
    public List<Notification> Notifications { get; } = [];
    public List<Touch> Touches { get; } = [];

    /// <summary>Every referral (each with its clock's pauses), and their history.</summary>
    public List<Referral> Referrals { get; } = [];
    public List<ReferralEvent> ReferralEvents { get; } = [];

    /// <summary>Where on the body each record with a site is about (its notes name it), by the record's ID.</summary>
    public Dictionary<Guid, string> Sites { get; } = [];

    /// <summary>Phase 3 to 5's setup: practices, sites and rooms, consent forms and letter templates.</summary>
    public CentreSetup Setup { get; init; } = null!;

    /// <summary>The specimen types and labs.</summary>
    public SpecimenSetup SpecimenSetup { get; init; } = null!;

    /// <summary>The skin cancer team's MDTs (meetings marked as MDTs once the MDT list went live), in order.</summary>
    public IReadOnlyList<Meeting> MdtMeetings { get; init; } = [];

    /// <summary>Specimens taken, results, consent records and MDT items, as the replays made them, to save together.</summary>
    public List<Specimen> Specimens { get; } = [];
    public List<Result> Results { get; } = [];
    public List<ConsentRecord> ConsentRecords { get; } = [];
    public List<MdtItem> MdtItems { get; } = [];

    /// <summary>Files MedApp made or took in (reports), with their bytes, stored once the records are saved.</summary>
    public List<Attachment> Attachments { get; } = [];
    public Dictionary<Guid, byte[]> FileBytes { get; } = [];

    /// <summary>Letters the replays planned, written and sent through the app's commands once the pathways are saved.</summary>
    public List<LetterPlan> LetterPlans { get; } = [];

    /// <summary>The next specimen number from the sequence (the seed restarts the sequence after it once saved).</summary>
    public long NextSpecimenNumber { get; set; } = 1;

    /// <summary>Pathways to delete (by an admin, later), with when.</summary>
    public List<(PatientPathway Pathway, DateTime AtUtc)> Deletions { get; } = [];

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
            ? new NotificationPreferences { UserId = member.Id, PathwayAssigned = false, UpdatedAtUtc = Timeline.NotificationsTurnedOffUtc }
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

    /// <summary>Whether a rule's owner could have monitoring rules at a moment (ManageMonitoring), as the app checks.</summary>
    public bool CanMonitorAt(Guid userId, DateTime utc) =>
        Staff.Any(s => s.Id == userId && s.PermissionsAt(utc).Contains(Permission.ManageMonitoring));

    /// <summary>
    /// What the app's monitor would send after a change (<c>PathwayMonitor</c>): its own matching
    /// (<c>PathwayMonitor.FindMatchesAsync</c>)
    /// and messages, answering its two questions (the switched-on rules for some events, and which owners are admins)
    /// as they stood at that moment, from memory. Its tasks are already complete, so waiting on them never blocks.
    /// </summary>
    public IEnumerable<Notification> Monitor(PatientPathway pathway, IReadOnlyList<PatientPathwayAuditEntry> entries, Guid actorUserId, DateTime atUtc)
    {
        var matches = PathwayMonitor.FindMatchesAsync(
                pathway,
                entries,
                actorUserId,
                (events, _) => Task.FromResult<IReadOnlyList<MonitoringRule>>(RulesEnabledAt(atUtc).Where(r => events.Contains(r.Event)).ToList()),
                (owners, _) => Task.FromResult<IReadOnlySet<Guid>>(owners.Where(o => CanMonitorAt(o, atUtc)).ToHashSet()),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        return matches.Select(m => MonitoringNotifications.Create(m.OwnerUserId, m.Activity, pathway, atUtc)).ToList();
    }
}
