using MedApp.Domain.Notepads;

namespace MedApp.Application.Notepads.Repositories;

/// <summary>Staff's notepads, one each, by user ID.</summary>
public interface INotepadRepository
{
    /// <summary>A user's notepad, tracked so <see cref="SaveAsync"/> saves changes to it, or null if they have none.</summary>
    Task<Notepad?> GetForUpdateAsync(Guid userId, CancellationToken ct);

    /// <summary>Adds a new notepad and saves. Of two first saves at once, the later is a 409.</summary>
    Task AddAsync(Notepad notepad, CancellationToken ct);

    /// <summary>
    /// Saves changes to a notepad loaded with <see cref="GetForUpdateAsync"/>. If it was saved elsewhere since it was
    /// loaded (its row version), nothing is saved and it's a 409.
    /// </summary>
    Task SaveAsync(CancellationToken ct);

    /// <summary>Deletes a notepad loaded with <see cref="GetForUpdateAsync"/> (it has expired), and saves.</summary>
    Task DeleteAsync(Notepad notepad, CancellationToken ct);
}
