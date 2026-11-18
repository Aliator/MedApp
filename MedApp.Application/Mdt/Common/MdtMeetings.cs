using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Mdt.Common;

/// <summary>
/// Meetings marked as a specialty's MDT (decision 141): the specialty chosen must exist and, when newly chosen, not be
/// archived; a meeting with items on it keeps its specialty and stays an MDT; one with outcomes recorded can't be
/// cancelled (cancelling one without clears its items' meeting, keeping them listed: decision 144).
/// </summary>
public static class MdtMeetings
{
    public const string ArchivedSpecialtyMessage = "That specialty is archived: choose another.";

    /// <summary>
    /// The specialty a meeting command makes it an MDT of, or null when it isn't one. <paramref name="current"/> is the
    /// meeting's own MDT specialty (null for a new or ordinary meeting), kept when the command doesn't say.
    /// </summary>
    public static async Task<Guid?> ChooseSpecialtyAsync(
        IMeetingCommand command, Guid? current, ISpecialtyRepository specialties, CancellationToken ct)
    {
        if (command.IsMdt is null)
            return current;
        if (command.IsMdt == false || command.SpecialtyId is not { } id)
            return null;
        if (id == current)
            return id;

        var specialty = await specialties.GetAsync(id, ct)
                        ?? throw new NotFoundException($"Specialty '{id}' was not found.");
        if (specialty.IsArchived)
            throw new ConflictException(ArchivedSpecialtyMessage);
        return id;
    }

    /// <summary>A 409 when a change would take an MDT with items on it away from its specialty.</summary>
    public static async Task EnsureCanChangeAsync(Meeting meeting, Guid? newSpecialtyId, IMdtItemRepository items, CancellationToken ct)
    {
        if (!meeting.IsMdt || newSpecialtyId == meeting.SpecialtyId)
            return;
        var (open, discussed) = await items.CountOnMeetingAsync(meeting.Id, ct);
        if (open + discussed > 0)
            throw new ConflictException(MdtRules.HasItemsMessage);
    }

    /// <summary>A 409 when an MDT has outcomes recorded on it.</summary>
    public static async Task EnsureCanCancelAsync(Meeting meeting, IMdtItemRepository items, CancellationToken ct)
    {
        if (!meeting.IsMdt)
            return;
        var (_, discussed) = await items.CountOnMeetingAsync(meeting.Id, ct);
        if (discussed > 0)
            throw new ConflictException(MdtRules.HasOutcomesMessage);
    }
}
