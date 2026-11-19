using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Lab;

namespace MedApp.Application.Lab.Common;

/// <summary>
/// What lab case handlers share (the light demo's <c>findCase</c>, <c>requireLabWork</c> and <c>forWork</c>): finding a
/// case behind the team walls, readying one for work, and its response.
/// </summary>
public sealed class LabCaseAccess(
    ILabCaseRepository repository,
    ITeamBoundary boundary,
    ITeamRepository teams,
    IIdentityReadService identity)
{
    public static NotFoundException NotFound(Guid id) => new($"Lab case '{id}' was not found.");

    /// <summary>The lab team's ID (the one lab not archived), or null without one.</summary>
    public async Task<Guid?> LabTeamIdAsync(CancellationToken ct) => (await teams.GetLabAsync(ct))?.Id;

    /// <summary>The case, tracked or not, or a 404: also behind the viewer's team wall (recorded). Returns why it's seen.</summary>
    public async Task<(LabCase Case, TeamAccessKind Access)> FindAsync(
        Guid id, PathwayActor viewer, Guid? labTeamId, bool forUpdate, CancellationToken ct)
    {
        var labCase = (forUpdate ? await repository.GetForUpdateAsync(id, ct) : await repository.GetByIdAsync(id, ct))
                      ?? throw NotFound(id);
        var access = await boundary.EnsureCaseVisibleAsync(labCase, viewer.Teams, labTeamId, NotFound(id).Message, ct);
        return (labCase, access);
    }

    /// <summary>Refuses work on cases to anyone but the lab team (admins read only): a 409.</summary>
    public static void EnsureWorksCases(PathwayActor actor, Guid? labTeamId)
    {
        if (!LabCaseRules.WorksCases(actor, labTeamId))
            throw new ConflictException(LabCaseRules.LabWorkMessage);
    }

    /// <summary>
    /// An open case to work on, tracked, from the version the page showed: the lab team only (409), seen (404), the
    /// record not anonymised, the version current and the case open (409s).
    /// </summary>
    public async Task<(LabCase Case, Guid? LabTeamId)> ForWorkAsync(Guid id, int expectedVersion, PathwayActor actor, CancellationToken ct)
    {
        var labTeamId = await LabTeamIdAsync(ct);
        EnsureWorksCases(actor, labTeamId);
        var (labCase, _) = await FindAsync(id, actor, labTeamId, forUpdate: true, ct);
        LabCaseRules.EnsureCanWork(labCase, expectedVersion);
        return (labCase, labTeamId);
    }

    /// <summary>The case's response, with what <paramref name="viewer"/> may do.</summary>
    public async Task<LabCaseResponse> ResponseAsync(
        LabCase labCase, PathwayActor viewer, Guid? labTeamId, DateTime nowUtc, CancellationToken ct)
    {
        var usernames = await identity.GetUsernamesByIdAsync(LabCaseMapper.PeopleIn(labCase), ct);
        return LabCaseMapper.ToResponse(labCase, usernames, viewer, labTeamId, nowUtc);
    }
}
