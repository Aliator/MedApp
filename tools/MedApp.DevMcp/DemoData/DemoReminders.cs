using MedApp.Application.Calendar.Common;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Sites;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Appointment reminders over the last four weeks (<c>docs/centre-phases-3-5-plan.md</c>, section 3.5), made by the
/// app's own factory (<see cref="PatientMessageFactory"/>: the patient's choice of channel, their mobile or email, the
/// fixed wording that never says why) two days before each appointment, as the reminders job makes them, and recorded
/// as the offline transport sends them: they went nowhere. Most were delivered, a few failed, and patients who gave no
/// consent (or no mobile) have a Not sent record saying so. An appointment booked after its reminder was due has none.
/// <para>
/// The setting that sends them (<c>PatientMessages:Mode</c>) stays as the deployment has it (Off unless someone turns it
/// on): this is the history of a trial in Offline mode, so the appointment pages show what a reminder looks like.
/// </para>
/// </summary>
public static class DemoReminders
{
    public static async Task<object> CreateAsync(SeedServices services, DemoWorld world, CancellationToken ct)
    {
        var now = world.Timeline.NowUtc;
        var from = now.AddDays(-28);
        var random = world.Random;
        var options = new PatientMessageOptions { SenderName = "The centre (demo)", ContactPhone = "01632 960001" };
        var site = new MessageSite(Site.MainSiteName, ["2 Larkfold Avenue", "Brackenmoor"], "ZZ1 4QL", "01632 960001");

        var due = world.Appointments
            .Where(a => a.StartUtc is { } start && start >= from && start < now.AddDays(2)
                        && a.Status is AppointmentStatus.Attended or AppointmentStatus.DidNotAttend or AppointmentStatus.Booked)
            .ToList();
        var patientIds = due.Select(a => a.PatientId).Distinct().ToList();
        var patients = await services.Run(sp => sp.GetRequiredService<MedAppDbContext>().Patients.AsNoTracking()
            .Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct));
        var bookedAt = world.AppointmentEvents
            .Where(e => e.Action == AppointmentEventAction.Booked)
            .GroupBy(e => e.AppointmentId)
            .ToDictionary(g => g.Key, g => g.Max(e => e.AtUtc));

        var messages = new List<PatientMessage>();
        foreach (var appointment in due.OrderBy(a => a.StartUtc))
        {
            var start = appointment.StartUtc!.Value;
            var at = DemoTimeline.At(UkTime.DateOf(start).AddDays(-2), 9, random.Next(0, 50));
            if (at >= now || !bookedAt.TryGetValue(appointment.Id, out var booked) || booked > at
                || !patients.TryGetValue(appointment.PatientId, out var patient)
                || patient.RestrictedAtUtc < at || patient.AnonymisedAtUtc is not null)
                continue;

            foreach (var message in PatientMessageFactory.For(patient, appointment, PatientMessageKind.AppointmentReminder, site, options, at))
            {
                if (message.Status == PatientMessageStatus.Pending)
                {
                    // Sent through the offline transport a minute or two later; most then confirmed delivered.
                    var sentAt = at.AddMinutes(random.Next(1, 4));
                    message.SendAttempts = 1;
                    if (random.NextDouble() < 0.05)
                    {
                        message.Status = PatientMessageStatus.Failed;
                        message.StatusReason = PatientMessageReasons.Rejected;
                        message.StatusAtUtc = sentAt;
                    }
                    else
                    {
                        message.Status = message.Channel == PatientMessageChannel.Text && random.NextDouble() < 0.9
                            ? PatientMessageStatus.Delivered
                            : PatientMessageStatus.Sent;
                        message.SentAtUtc = sentAt;
                        message.ProviderReference = $"offline-{message.Id:N}"[..20];
                        message.StatusAtUtc = message.Status == PatientMessageStatus.Delivered ? sentAt.AddMinutes(random.Next(1, 20)) : sentAt;
                    }
                }

                messages.Add(message);
            }
        }

        await services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            db.PatientMessages.AddRange(messages);
            await db.SaveChangesAsync(ct);
        });

        return new
        {
            reminders = messages.Count,
            byStatus = messages.GroupBy(m => m.Status).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            notSentNoConsent = messages.Count(m => m.StatusReason == PatientMessageReasons.NoConsent),
        };
    }
}
