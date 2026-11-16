using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Common;

public static class AppointmentRepositoryExtensions
{
    /// <summary>The appointment, tracked for changes, or a 404.</summary>
    public static async Task<Appointment> GetForUpdateOrThrowAsync(
        this IAppointmentRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetForUpdateAsync(id, ct)
        ?? throw new NotFoundException($"Appointment '{id}' was not found.");

    /// <summary>The username of the appointment's member of staff, for its history; null when it has none or they're deleted.</summary>
    public static async Task<string?> GetStaffUsernameAsync(
        this IIdentityReadService identity, Appointment appointment, CancellationToken ct) =>
        appointment.StaffUserId is { } staffUserId
            ? (await identity.GetUsernamesByIdAsync([staffUserId], ct)).GetValueOrDefault(staffUserId)
            : null;
}
