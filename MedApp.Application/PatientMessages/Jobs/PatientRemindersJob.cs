using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.PatientMessages.Common;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedApp.Application.PatientMessages.Jobs;

/// <summary>
/// Patient reminders (centre plan decisions 128 to 133), every 15 minutes, while patient messages are on:
/// <list type="number">
/// <item><b>Makes reminders</b> from 09:00 to 19:00 UK time: one for each Booked appointment starting more than two hours
/// and no more than <see cref="ReminderOptions.DaysBefore"/> days away, on each channel the patient chose, unless it has
/// one about that start already.</item>
/// <item><b>Makes missed-appointment messages</b>, when <see cref="ReminderOptions.MissedMessage"/> is on, from 10:00 on
/// the working day after a Did not attend (only that day).</item>
/// <item><b>Sends what's waiting</b>: a move or cancellation at once, reminders and missed-appointment messages only from
/// 09:00 to 19:00, each checked again first (<see cref="PatientMessageRules.WhyNotToSend"/>), each saved as it goes.</item>
/// <item><b>Asks about delivery</b> of what it sent in the last three days.</item>
/// </list>
/// Each message is unique per appointment, kind, channel and start, so a run cut short repeats nothing it saved. It logs
/// counts only, never a patient, destination or text. A reminder is a courtesy (hazard H-54): nothing relies on one.
/// </summary>
public sealed class PatientRemindersJob(
    IPatientMessageRepository repository,
    IPatientMessageTransport transport,
    IClosedDayRepository closedDays,
    IOptions<PatientMessageOptions> options,
    IOptions<ReminderOptions> reminderOptions,
    ILogger<PatientRemindersJob> logger) : IScheduledJob
{
    public const string JobName = "patient-reminders";

    /// <summary>The most messages one run makes, sends or asks about, each: the rest wait for the next run.</summary>
    public const int BatchSize = 200;

    public string Name => JobName;

    public string Title => "Patient reminders";

    public string Description
    {
        get
        {
            if (!options.Value.IsOn)
                return "Off (PatientMessages:Mode): makes and sends no texts or emails to patients.";

            var reminders = reminderOptions.Value;
            var sender = options.Value.Mode == PatientMessageMode.Live
                ? "through GOV.UK Notify"
                : "offline, to the development mailbox, never to patients";
            return $"Texts and emails patients {sender}: reminders up to {reminders.DaysBefore} days before a booked appointment, "
                   + "news of moves and cancellations staff ask for"
                   + (reminders.MissedMessage ? ", and a message the working day after a missed appointment" : string.Empty)
                   + ". Reminders go only from 09:00 to 19:00.";
        }
    }

    public JobSchedule Schedule { get; } = JobSchedule.Every(TimeSpan.FromMinutes(15));

    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (!options.Value.IsOn)
            return 0;

        var made = 0;
        if (PatientMessageRules.InSendingHours(nowUtc))
        {
            made += await MakeRemindersAsync(nowUtc, cancellationToken);
            if (reminderOptions.Value.MissedMessage)
                made += await MakeMissedAsync(nowUtc, cancellationToken);
        }

        var sending = await SendAsync(nowUtc, cancellationToken);
        var delivery = await CheckDeliveryAsync(nowUtc, cancellationToken);

        logger.LogInformation(
            "Patient reminders: {Made} messages made, {Sent} sent, {Failed} failed, {Cancelled} no longer applied, {Updated} delivery updates.",
            made, sending.Sent, sending.Failed, sending.Cancelled, delivery);

        return made + sending.Sent + sending.Failed + sending.Cancelled + delivery;
    }

    private async Task<int> MakeRemindersAsync(DateTime nowUtc, CancellationToken ct)
    {
        var due = await repository.GetDueRemindersAsync(
            nowUtc + PatientMessageRules.ReminderAtLeast,
            nowUtc.AddDays(reminderOptions.Value.DaysBefore),
            BatchSize,
            ct);
        return await MakeAsync(due, PatientMessageKind.AppointmentReminder, nowUtc, ct);
    }

    /// <summary>
    /// Missed-appointment messages for the appointments whose next working day is today, from 10:00: those from the last
    /// working day up to yesterday (a weekend's, or a closed day's, too).
    /// </summary>
    private async Task<int> MakeMissedAsync(DateTime nowUtc, CancellationToken ct)
    {
        if (UkTime.TimeOf(nowUtc) < PatientMessageRules.MissedFrom)
            return 0;

        var today = UkTime.DateOf(nowUtc);
        var closed = new ClosedDaySet(await closedDays.GetAsync(today.AddDays(-14), today, ct));
        if (!IsWorkingDay(today, closed))
            return 0;

        var lastWorkingDay = today.AddDays(-1);
        while (!IsWorkingDay(lastWorkingDay, closed) && lastWorkingDay > today.AddDays(-14))
            lastWorkingDay = lastWorkingDay.AddDays(-1);

        var due = await repository.GetDueMissedAsync(
            DueDates.StartOfDayUtc(lastWorkingDay), DueDates.StartOfDayUtc(today), BatchSize, ct);
        return await MakeAsync(due, PatientMessageKind.MissedAppointment, nowUtc, ct);
    }

    /// <summary>A working day for these messages: Monday to Friday, not closed everywhere.</summary>
    public static bool IsWorkingDay(DateOnly date, ClosedDaySet closed) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !closed.IsClosed(date);

    private async Task<int> MakeAsync(IReadOnlyList<Appointment> appointments, PatientMessageKind kind, DateTime nowUtc, CancellationToken ct)
    {
        if (appointments.Count == 0)
            return 0;

        var sites = await repository.GetSitesForRoomsAsync(appointments.Select(a => a.RoomId).OfType<Guid>().Distinct(), ct);
        var messages = appointments
            .SelectMany(a => PatientMessageFactory.For(
                a.Patient, a, kind, a.RoomId is { } roomId ? sites.GetValueOrDefault(roomId) : null, options.Value, nowUtc))
            .ToList();

        await repository.AddAsync(messages, ct);
        return messages.Count;
    }

    private async Task<(int Sent, int Failed, int Cancelled)> SendAsync(DateTime nowUtc, CancellationToken ct)
    {
        var pending = await repository.GetPendingAsync(BatchSize, ct);
        if (pending.Count == 0)
            return (0, 0, 0);

        var appointments = await repository.GetAppointmentsAsync(pending.Select(m => m.AppointmentId).Distinct(), ct);
        int sent = 0, failed = 0, cancelled = 0;

        foreach (var message in pending)
        {
            if (!appointments.TryGetValue(message.AppointmentId, out var appointment))
            {
                Close(message, PatientMessageStatus.Cancelled, PatientMessageReasons.NoLongerApplies, nowUtc);
                cancelled++;
                await repository.SaveAsync(ct);
                continue;
            }

            if (PatientMessageRules.WhyNotToSend(message, appointment, appointment.Patient, nowUtc) is { } reason)
            {
                Close(message, PatientMessageStatus.Cancelled, reason, nowUtc);
                cancelled++;
                await repository.SaveAsync(ct);
                continue;
            }

            if (!PatientMessageRules.CanSendNow(message.Kind, nowUtc))
                continue;

            // To where they are now, as the record says (a mobile corrected since the message was made, say).
            message.Destination = PatientMessageRules.DestinationFor(appointment.Patient, message.Channel)!;

            PatientMessageSendResult result;
            try
            {
                result = await transport.SendAsync(message, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // The type only: a provider's message can quote the number or address.
                logger.LogWarning("A patient message couldn't be sent this time ({ExceptionType}).", e.GetType().Name);
                message.SendAttempts++;
                if (message.SendAttempts >= PatientMessageRules.MaxSendAttempts)
                {
                    Close(message, PatientMessageStatus.Failed, PatientMessageReasons.TooManyAttempts, nowUtc);
                    failed++;
                }

                await repository.SaveAsync(ct);
                continue;
            }

            if (result.Outcome == PatientMessageSendOutcome.TryLater)
                break;

            if (result.Outcome == PatientMessageSendOutcome.Rejected)
            {
                Close(message, PatientMessageStatus.Failed, result.Reason ?? PatientMessageReasons.Rejected, nowUtc);
                failed++;
            }
            else
            {
                message.Status = PatientMessageStatus.Sent;
                message.ProviderReference = result.Reference;
                message.SentAtUtc = nowUtc;
                message.StatusAtUtc = nowUtc;
                sent++;
            }

            // Saved as it goes, so a run cut short doesn't send it again.
            await repository.SaveAsync(ct);
        }

        return (sent, failed, cancelled);
    }

    private async Task<int> CheckDeliveryAsync(DateTime nowUtc, CancellationToken ct)
    {
        var awaiting = await repository.GetAwaitingDeliveryAsync(nowUtc - PatientMessageRules.DeliveryCheckedFor, BatchSize, ct);
        var updated = 0;
        foreach (var message in awaiting)
        {
            PatientMessageDelivery delivery;
            try
            {
                delivery = await transport.GetDeliveryAsync(message.ProviderReference!, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // The provider is away: the rest wait for the next run.
                logger.LogWarning("Delivery of patient messages couldn't be checked this time ({ExceptionType}).", e.GetType().Name);
                break;
            }

            message.StatusAtUtc = nowUtc;
            if (delivery.Status != PatientMessageStatus.Sent)
            {
                message.Status = delivery.Status;
                message.StatusReason = delivery.Reason;
                updated++;
            }
        }

        if (awaiting.Count > 0)
            await repository.SaveAsync(ct);
        return updated;
    }

    private static void Close(PatientMessage message, PatientMessageStatus status, string reason, DateTime nowUtc)
    {
        message.Status = status;
        message.StatusReason = reason;
        message.StatusAtUtc = nowUtc;
    }
}
