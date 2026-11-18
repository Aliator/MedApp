using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;

namespace MedApp.Application.Clinics.Common;

/// <summary>The rooms and specialties a session or pattern may name: they exist (404), and aren't archived (409) unless it already has them.</summary>
public static class ClinicChoices
{
    /// <summary>A room for a session or pattern: it exists (404) and isn't archived (409) unless it's <paramref name="currentRoomId"/>.</summary>
    public static async Task CheckRoomAsync(
        IClinicRepository repository, Guid roomId, DateOnly date, Guid? currentRoomId, CancellationToken ct)
    {
        var day = await repository.GetRoomDayAsync(roomId, date, ct)
                  ?? throw new NotFoundException($"Room '{roomId}' was not found.");
        if (day.Room.IsArchived && roomId != currentRoomId)
            throw new ConflictException(ClinicRules.RoomArchived(day.Room));
    }

    /// <summary>A specialty for a session or pattern: it exists (404) and isn't archived (409) unless it's <paramref name="currentSpecialtyId"/>.</summary>
    public static async Task CheckSpecialtyAsync(
        ISpecialtyRepository repository, Guid specialtyId, Guid? currentSpecialtyId, CancellationToken ct) =>
        await SpecialtyRules.CheckChosenAsync(
            repository,
            [specialtyId],
            currentSpecialtyId is { } current ? new HashSet<Guid> { current } : new HashSet<Guid>(),
            ct);
}
