using MediatR;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
using MedApp.Application.Patients.Commands.AnonymisePatient;
using MedApp.Application.Patients.Commands.KeepPatientRecord;
using MedApp.Application.Patients.Commands.SetPatientFlag;
using MedApp.Application.Patients.Queries.ExportPatient;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;
using MedApp.Domain.Appointments;
using MedApp.DevMcp.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The records' privacy history and the admins' clear-ups, all through the app's own commands, at their moments:
/// two restricted records (one with a procedure under way, which staff can still pause), two disputed, one imported
/// record anonymised after its retention review, two kept past their period with a reason, two subject access
/// exports, two cancelled procedures deleted, and the leaver's account deleted on their last day.
/// </summary>
public static class DemoPrivacy
{
    public static async Task<object> CreateAsync(SeedServices services, DemoWorld world, DemoPatients patients, CancellationToken ct)
    {
        var random = world.Random;
        var now = world.Timeline.NowUtc;
        var lead = world.Lead;
        IMediator Mediator(IServiceProvider sp) => sp.GetRequiredService<IMediator>();

        // Cancelled procedures deleted by an admin; their audit stays, shown to admins on the timeline.
        foreach (var (procedure, at) in world.Deletions)
        {
            services.At(at, lead.ActivityActor);
            await services.Run(sp => Mediator(sp).Send(new DeletePatientProcedureCommand(procedure.Id, lead.Actor), ct));
        }

        DateTime LastActivity(Guid patientId) =>
            world.Audit.Where(a => a.PatientId == patientId).Select(a => a.AtUtc)
                .Concat(world.AppointmentEvents.Where(e => e.PatientId == patientId).Select(e => e.AtUtc))
                .DefaultIfEmpty(DateTime.MinValue).Max();
        bool HasUpcoming(Guid patientId) =>
            world.Appointments.Any(a => a.PatientId == patientId && a.Status is AppointmentStatus.Booked or AppointmentStatus.Requested);

        var active = patients.All.Where(p => p.Role == PatientRole.Active && p.Id != Guid.Empty).ToList();
        var flagged = new List<PatientSeed>();

        // Restricted, with a procedure under way (quiet for a fortnight, nothing booked): staff can still block or cancel it.
        var withOpenWork = active.Where(p =>
                world.Procedures.Any(x => x.PatientId == p.Id && x.Status is Domain.Procedures.PatientProcedures.PatientProcedureStatus.InProgress)
                && LastActivity(p.Id) < now.AddDays(-12) && !HasUpcoming(p.Id))
            .OrderBy(_ => random.Next()).FirstOrDefault();
        if (withOpenWork is not null)
            await Flag(withOpenWork, PatientFlag.Restricted, Later(LastActivity(withOpenWork.Id).AddDays(2), now.AddDays(-9)),
                "The patient has asked us to restrict use of their record while a complaint about how it was shared is looked into.");

        // Restricted, everything closed.
        var closed = active.Except(flagged).Where(p =>
                world.Procedures.Where(x => x.PatientId == p.Id).All(x => ProcedureWorkflow.IsClosed(x.Status))
                && LastActivity(p.Id) < now.AddDays(-40) && !HasUpcoming(p.Id))
            .OrderBy(_ => random.Next()).FirstOrDefault();
        if (closed is not null)
            await Flag(closed, PatientFlag.Restricted, Later(LastActivity(closed.Id).AddDays(3), now.AddDays(-35)),
                "The patient contests the accuracy of their contact history; processing restricted while it is checked.");

        // Disputed: a banner only.
        var disputeReasons = new[]
        {
            "The patient disputes the recorded date of birth.",
            "The patient says the email address on file is not theirs.",
        };
        var disputed = active.Except(flagged).OrderBy(_ => random.Next()).Take(2).ToList();
        for (var i = 0; i < disputed.Count; i++)
            await Flag(disputed[i], PatientFlag.Disputed, DemoTimeline.WorkingMoment(now.AddDays(i == 0 ? -28 : -12), 11, 20), disputeReasons[i]);

        // Subject access exports, by an admin: the first disputed patient's, and another.
        var exports = 0;
        foreach (var (patient, daysAgo) in disputed.Take(1).Select(p => (p, 27)).Append((active.Except(flagged).OrderBy(_ => random.Next()).First(), 9)))
        {
            services.At(DemoTimeline.WorkingMoment(now.AddDays(-daysAgo), 14, 5), lead.ActivityActor);
            await services.Run(sp => Mediator(sp).Send(new ExportPatientQuery(patient.Id), ct));
            exports++;
        }

        // The retention review: one imported record anonymised, two kept until next year.
        var due = patients.All.Where(p => p.Role == PatientRole.ImportedDue).ToList();
        var anonymised = due.FirstOrDefault(p => !p.IsChildAt(now));
        if (anonymised is not null)
        {
            services.At(DemoTimeline.WorkingMoment(now.AddDays(-21), 10, 40), lead.ActivityActor);
            await services.Run(sp => Guards.Validated(() => Mediator(sp).Send(new AnonymisePatientCommand(
                anonymised.Id, nameof(AnonymisationReason.RetentionPeriodEnded), "Reviewed at the records meeting.", anonymised.FullName), ct)));
        }

        var keepReasons = new[]
        {
            "Linked to an ongoing claim: keep until it is settled.",
            "Family history relevant to a relative's current care; review next year.",
        };
        var kept = due.Where(p => p != anonymised).Take(2).ToList();
        for (var i = 0; i < kept.Count; i++)
        {
            services.At(DemoTimeline.WorkingMoment(now.AddDays(-14 + i), 11, 0), lead.ActivityActor);
            await services.Run(sp => Mediator(sp).Send(new KeepPatientRecordCommand(
                kept[i].Id, world.Timeline.Today.AddYears(1).AddDays(-30 * i), keepReasons[i]), ct));
        }

        // The leaver's account, deleted on their last day: their name stays only on closed work, as "Deleted user".
        var leaver = world.Staff.Single(s => s.Kind == StaffKind.Leaver);
        services.At(leaver.LeftUtc!.Value.AddMinutes(30), lead.ActivityActor);
        var deletedLeaver = await services.Run(sp => Mediator(sp).Send(
            new DeleteUserCommand(leaver.Username, lead.Username, TestAdminTools.Password), ct));
        Guards.Check(deletedLeaver, $"Delete {leaver.Username}");

        return new
        {
            restricted = flagged.Count(p => p.Restricted),
            disputed = disputed.Count,
            anonymised = anonymised is null ? 0 : 1,
            retentionKept = kept.Count,
            exports,
            deletedProcedures = world.Deletions.Count,
            deletedAccounts = new[] { leaver.Username },
        };

        async Task Flag(PatientSeed patient, PatientFlag flag, DateTime at, string reason)
        {
            services.At(at, lead.ActivityActor);
            await services.Run(sp => Guards.Validated(() => Mediator(sp).Send(
                new SetPatientFlagCommand(patient.Id, flag, true, reason, ConfirmOpenProcedures: true), ct)));
            if (flag == PatientFlag.Restricted)
                patient.Restricted = true;
            flagged.Add(patient);
        }

        static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;
    }
}
