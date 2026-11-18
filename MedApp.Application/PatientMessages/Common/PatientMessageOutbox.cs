using MedApp.Application.PatientMessages.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using Microsoft.Extensions.Options;

namespace MedApp.Application.PatientMessages.Common;

/// <summary>
/// Where handlers ask for a patient to be told of a change staff made (an appointment moved or cancelled, a clinic
/// session cancelled: decision 130). The messages are staged on the request's DbContext, so they're saved in the same
/// save as the change (<see cref="IPatientMessageRepository.Stage"/>), and the <c>patient-reminders</c> job sends them.
/// </summary>
public interface IPatientMessageOutbox
{
    /// <summary>Whether messages are made at all (<see cref="PatientMessageOptions.Mode"/> isn't Off).</summary>
    bool IsOn { get; }

    /// <summary>
    /// Stages the messages telling the appointment's patient (loaded with it) of <paramref name="kind"/>, for its start
    /// now: one per channel they chose, or one saying why none could go. Nothing when messages are off, or when this
    /// appointment already has a message of this kind about this start and room.
    /// </summary>
    Task StageAsync(Appointment appointment, PatientMessageKind kind, Guid requestedByUserId, DateTime nowUtc, CancellationToken ct);
}

public sealed class PatientMessageOutbox(IPatientMessageRepository repository, IOptions<PatientMessageOptions> options)
    : IPatientMessageOutbox
{
    public bool IsOn => options.Value.IsOn;

    public async Task StageAsync(
        Appointment appointment, PatientMessageKind kind, Guid requestedByUserId, DateTime nowUtc, CancellationToken ct)
    {
        if (!IsOn || appointment.StartUtc is not { } startUtc)
            return;

        if (await repository.ExistsAsync(appointment.Id, kind, startUtc, appointment.RoomId, ct))
            return;

        MessageSite? site = null;
        if (appointment.RoomId is { } roomId)
            (await repository.GetSitesForRoomsAsync([roomId], ct)).TryGetValue(roomId, out site);

        repository.Stage(PatientMessageFactory.For(
            appointment.Patient, appointment, kind, site, options.Value, nowUtc, requestedByUserId));
    }
}
