using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Mdt.Common;

/// <summary>
/// Meetings marked as a team's MDT (decision 141): the team chosen must exist and, when newly chosen, not be
/// archived; a meeting with items on it keeps its team and stays an MDT; one with outcomes recorded can't be
/// cancelled (cancelling one without clears its items' meeting, keeping them listed: decision 144).
/// </summary>
public static class MdtMeetings
{
    public const string ArchivedTeamMessage = "That team is archived: choose another.";

    /// <summary>
    /// The team a meeting command makes it an MDT of, or null when it isn't one. <paramref name="current"/> is the
    /// meeting's own MDT team (null for a new or ordinary meeting), kept when the command doesn't say.
    /// </summary>
    public static async Task<Guid?> ChooseTeamAsync(
        IMeetingCommand command, Guid? current, ITeamRepository teams, CancellationToken ct)
    {
        if (command.IsMdt is null)
            return current;
        if (command.IsMdt == false || command.TeamId is not { } id)
            return null;
        if (id == current)
            return id;

        var team = await teams.GetAsync(id, ct)
                        ?? throw new NotFoundException($"Team '{id}' was not found.");
        if (team.IsArchived)
            throw new ConflictException(ArchivedTeamMessage);
        return id;
    }

    /// <summary>A 409 when a change would take an MDT with items on it away from its team.</summary>
    public static async Task EnsureCanChangeAsync(Meeting meeting, Guid? newTeamId, IMdtItemRepository items, CancellationToken ct)
    {
        if (!meeting.IsMdt || newTeamId == meeting.TeamId)
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
