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
/// new starter, a surgeon's high-priority stages, blocked work, due date changes, and one switched off since), and the
/// service manager's once they became an admin.
/// </summary>
public static class DemoMonitoring
{
    public static async Task<List<RuleState>> CreateAsync(
        SeedServices services,
        IReadOnlyList<StaffMember> staff,
        IReadOnlyDictionary<Pathway, Guid> templates,
        DemoTimeline timeline,
        CancellationToken ct)
    {
        var lead = staff.Single(s => s.Kind == StaffKind.ClinicalLead);
        var manager = DemoStaff.Get(staff, DemoStaff.ManagerUsername);
        Guid Id(string username) => DemoStaff.Get(staff, username).Id;

        var leadRules = new (Guid? Subject, MonitoringEvent Event, string? MinimumPriority, Guid? Template)[]
        {
            (Id(DemoStaff.NewStarterUsername), MonitoringEvent.ProcedureCreated, null, null),
            (Id("r.calloway"), MonitoringEvent.StageFinished, "High", null),
            (Id("r.calloway"), MonitoringEvent.ProcedureBlocked, null, null),
            (Id("d.ashworth"), MonitoringEvent.ProcedureBlocked, null, null),
            (null, MonitoringEvent.ProcedureBlocked, null, templates[Pathway.Diagnostic]),
            (null, MonitoringEvent.DueDateChanged, null, null),
            // Switched off later: the lead stopped wanting every unlock by the service manager.
            (manager.Id, MonitoringEvent.StageUnlocked, null, null),
        };
        var managerRules = new (Guid? Subject, MonitoringEvent Event, string? MinimumPriority, Guid? Template)[]
        {
            (null, MonitoringEvent.ProcedureCreated, "Urgent", templates[Pathway.Diagnostic]),
            (null, MonitoringEvent.ProcedureCancelled, null, null),
            (null, MonitoringEvent.ProcedureCompleted, null, templates[Pathway.DayCase]),
            (lead.Id, MonitoringEvent.ProcedureAssigned, null, null),
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

        async Task Add(StaffMember owner, DateTime at, (Guid? Subject, MonitoringEvent Event, string? MinimumPriority, Guid? Template)[] wanted)
        {
            foreach (var (rule, i) in wanted.Select((r, i) => (r, i)))
            {
                var when = at.AddMinutes(3 * i);
                services.At(when, owner.ActivityActor);
                var response = await services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(
                    new CreateMonitoringRuleCommand(owner.Id, rule.Subject, rule.Event.ToString(), rule.MinimumPriority, rule.Template, null, true), ct)));
                created.Add((response.Id, when));
            }
        }
    }
}
