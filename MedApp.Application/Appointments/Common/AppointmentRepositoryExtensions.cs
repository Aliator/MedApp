using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Appointments;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Appointments.Common;

public static class AppointmentRepositoryExtensions
{
    /// <summary>
    /// The appointment, tracked for changes, or a 404. A 409 if its patient's record is restricted or anonymised
    /// (<see cref="PatientRules"/>), since then nothing about it can change, as with their procedures. Every change to
    /// an appointment loads it here, or, only to cancel it, through <see cref="GetForCancelOrThrowAsync"/>.
    /// </summary>
    public static async Task<Appointment> GetForUpdateOrThrowAsync(
        this IAppointmentRepository repository, Guid id, CancellationToken ct)
    {
        var appointment = await repository.GetForUpdateAsync(id, ct)
                          ?? throw new NotFoundException($"Appointment '{id}' was not found.");
        PatientRules.EnsureCanChange(appointment.Patient);
        return appointment;
    }

    /// <summary>
    /// The appointment, tracked for changes, to cancel it, or a 404. Allowed while its patient's record is restricted,
    /// so care can be paused safely (as a restricted record's procedures can be cancelled); a 409 if it's anonymised.
    /// </summary>
    public static async Task<Appointment> GetForCancelOrThrowAsync(
        this IAppointmentRepository repository, Guid id, CancellationToken ct)
    {
        var appointment = await repository.GetForUpdateAsync(id, ct)
                          ?? throw new NotFoundException($"Appointment '{id}' was not found.");
        PatientRules.EnsureCanPause(appointment.Patient);
        return appointment;
    }

    /// <summary>The username of the appointment's member of staff, for its history; null when it has none or they're deleted.</summary>
    public static async Task<string?> GetStaffUsernameAsync(
        this IIdentityReadService identity, Appointment appointment, CancellationToken ct) =>
        appointment.StaffUserId is { } staffUserId
            ? (await identity.GetUsernamesByIdAsync([staffUserId], ct)).GetValueOrDefault(staffUserId)
            : null;

    /// <summary>
    /// Why an account can't stop being staff (be deleted, or lose every staff role): it has Booked appointments still
    /// to come, which would be left on a calendar nobody can open. Null if it has none. Deleting an account and
    /// changing its roles both ask this, so they refuse alike.
    /// </summary>
    public static async Task<IdentityError?> WhyCantStopBeingStaffAsync(
        this IAppointmentRepository repository, Guid userId, string username, DateTime nowUtc, CancellationToken ct)
    {
        var upcoming = await repository.CountUpcomingForStaffAsync(userId, nowUtc, ct);
        return upcoming > 0 ? IdentityErrors.HasUpcomingAppointments(username, upcoming) : null;
    }
}
