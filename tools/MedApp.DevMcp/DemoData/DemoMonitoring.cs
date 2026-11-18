using MediatR;
using MedApp.Application.Monitoring.Commands.CreateMonitoringRule;
using MedApp.Application.Monitoring.Commands.SetMonitoringRuleEnabled;
using MedApp.Domain.Monitoring;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The admins' monitoring rules, made through the app's commands when each admin set them up: the lead's (about the
/// new starter, the dermatologist's treatment on high-priority work, the lab's reports on urgent work, blocked work,
/// due date changes, and one switched off since), and the service manager's once they became an admin.
/// </summary>
public static class DemoMonitoring
{
    /// <summary>A rule to make: whom it watches (or anyone), the event, and its optional conditions.</summary>
    private sealed record Rule(Guid? Subject, MonitoringEvent Event, string? MinimumPriority = null, Guid? Template = null, Guid? Stage = null);

    public static async Task<List<RuleState>> CreateAsync(
        SeedServices services,
        IReadOnlyList<StaffMember> staff,
        IReadOnlyDictionary<Pathway, Guid> templates,
        IReadOnlyDictionary<string, Guid> stageDefinitions,
        DemoTimeline timeline,
        CancellationToken ct)
    {
        var lead = staff.Single(s => s.Kind == StaffKind.ClinicalLead);
        var manager = DemoStaff.Get(staff, DemoStaff.ManagerUsername);
        Guid Id(string username) => DemoStaff.Get(staff, username).Id;

        var leadRules = new Rule[]
        {
            new(Id(DemoStaff.NewStarterUsername), MonitoringEvent.ProcedureCreated),
            // Two rules that name a stage: the dermatologist's treatment on high-priority work, and the lab's reports on urgent work.
            new(Id(DemoStaff.DermatologistUsername), MonitoringEvent.StageFinished, "High", Stage: stageDefinitions[DemoPathways.Treatment]),
            new(null, MonitoringEvent.StageFinished, "Urgent", Stage: stageDefinitions[DemoPathways.Histology]),
            new(Id(DemoStaff.DermatologistUsername), MonitoringEvent.ProcedureBlocked),
            new(Id(DemoStaff.SkinGpUsername), MonitoringEvent.ProcedureBlocked),
            new(null, MonitoringEvent.ProcedureBlocked, Template: templates[Pathway.PostMenopausalBleeding]),
            new(null, MonitoringEvent.DueDateChanged),
            // Switched off later: the lead stopped wanting every unlock by the service manager.
            new(manager.Id, MonitoringEvent.StageUnlocked),
        };
        var managerRules = new Rule[]
        {
            new(null, MonitoringEvent.ProcedureCreated, "Urgent", templates[Pathway.PostMenopausalBleeding]),
            new(null, MonitoringEvent.ProcedureCancelled),
            new(null, MonitoringEvent.ProcedureCompleted, Template: templates[Pathway.Biomechanics]),
            new(lead.Id, MonitoringEvent.ProcedureAssigned),
        };

        var created = new List<(Guid Id, DateTime At)>();
        await Add(lead, timeline.MonitoringRulesUtc, leadRules);
        await Add(manager, timeline.TwoFactorEnabledUtc.AddDays(1), managerRules);

        // The last of the lead's rules switched off two weeks ago.
        var switchedOff = created[leadRules.Length - 1];
        var offAt = DemoTimeline.WorkingMoment(timeline.NowUtc.AddDays(-14), 15, 10);
        services.At(offAt, lead.ActivityActor);
        await services.Run(sp => sp.GetRequiredService<IMediator>().Send(new SetMonitoringRuleEnabledCommand(switchedOff.Id, lead.Id, false), ct));

        var rules = await services.Run(async sp =>
            await sp.GetRequiredService<MedAppDbContext>().MonitoringRules.AsNoTracking().ToListAsync(ct));
        return created
            .Select(c => new RuleState(rules.Single(r => r.Id == c.Id), c.At, c.Id == switchedOff.Id ? offAt : null))
            .ToList();

        async Task Add(StaffMember owner, DateTime at, Rule[] wanted)
        {
            foreach (var (rule, i) in wanted.Select((r, i) => (r, i)))
            {
                var when = at.AddMinutes(3 * i);
                services.At(when, owner.ActivityActor);
                var response = await services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(
                    new CreateMonitoringRuleCommand(owner.Id, rule.Subject, rule.Event.ToString(), rule.MinimumPriority, rule.Template, rule.Stage, true), ct)));
                created.Add((response.Id, when));
            }
        }
    }
}
