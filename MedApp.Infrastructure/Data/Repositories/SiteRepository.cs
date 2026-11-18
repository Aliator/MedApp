using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Domain.Sites;
using MedApp.Infrastructure.Data.Configurations.Sites;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Sites and rooms. Names are unique (sites) and unique within a site (rooms), by unique indexes.</summary>
public sealed class SiteRepository(MedAppDbContext context) : ISiteRepository
{
    /// <summary>SQL Server's error for a statement a foreign key refuses.</summary>
    private const int ForeignKeyViolation = 547;

    public async Task<IReadOnlyList<Site>> GetSitesAsync(bool includeArchived, CancellationToken ct) =>
        await context.Sites
            .AsNoTracking()
            .Where(s => includeArchived || !s.IsArchived)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task<Site?> GetSiteAsync(Guid id, CancellationToken ct) =>
        await context.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Room>> GetRoomsAsync(Guid? siteId, bool includeArchived, CancellationToken ct) =>
        await context.Rooms
            .AsNoTracking()
            .Where(r => (siteId == null || r.SiteId == siteId) && (includeArchived || !r.IsArchived))
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

    public async Task<Room?> GetRoomAsync(Guid id, CancellationToken ct) =>
        await context.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<bool> SiteNameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await context.Sites.AnyAsync(s => s.Name.ToLower() == lower && (exceptId == null || s.Id != exceptId), ct);
    }

    public async Task<bool> RoomNameExistsAsync(Guid siteId, string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await context.Rooms.AnyAsync(
            r => r.SiteId == siteId && r.Name.ToLower() == lower && (exceptId == null || r.Id != exceptId), ct);
    }

    public async Task<int> CountOpenSitesAsync(CancellationToken ct) =>
        await context.Sites.CountAsync(s => !s.IsArchived, ct);

    public async Task<IReadOnlySet<Guid>> GetSitesInUseAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();

        var withRooms = await context.Rooms
            .Where(r => list.Contains(r.SiteId))
            .Select(r => r.SiteId)
            .Distinct()
            .ToListAsync(ct);

        var withClosedDays = await context.ClosedDays
            .Where(d => d.SiteId != null && list.Contains(d.SiteId.Value))
            .Select(d => d.SiteId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var withStaffHours = await context.StaffHours
            .Where(h => list.Contains(h.SiteId))
            .Select(h => h.SiteId)
            .Distinct()
            .ToListAsync(ct);

        return withRooms.Concat(withClosedDays).Concat(withStaffHours).ToHashSet();
    }

    // Nothing names a room yet: clinic sessions and appointments will (the centre plan's PR 23), and add their checks here.
    public Task<IReadOnlySet<Guid>> GetRoomsInUseAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    public async Task AddSiteAsync(Site site, CancellationToken ct)
    {
        context.Sites.Add(site);
        await SaveSiteAsync(site, ct);
    }

    public async Task UpdateSiteAsync(Site site, CancellationToken ct)
    {
        context.Sites.Update(site);
        await SaveSiteAsync(site, ct);
    }

    public async Task DeleteSiteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await context.Sites.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        }
        // A room or closed day named it after this request's check (the foreign keys refuse the delete).
        catch (SqlException e) when (e.Number == ForeignKeyViolation)
        {
            throw new ConflictException("Someone added a room or closed day to that site just now, so it can't be deleted. Archive it instead.");
        }
    }

    public async Task AddRoomAsync(Room room, CancellationToken ct)
    {
        context.Rooms.Add(room);
        await SaveRoomAsync(room, ct);
    }

    public async Task UpdateRoomAsync(Room room, CancellationToken ct)
    {
        context.Rooms.Update(room);
        await SaveRoomAsync(room, ct);
    }

    public async Task DeleteRoomAsync(Guid id, CancellationToken ct) =>
        await context.Rooms.Where(r => r.Id == id).ExecuteDeleteAsync(ct);

    // Another request took the name after this one's check.
    private async Task SaveSiteAsync(Site site, CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(SiteConfiguration.NameIndex))
        {
            context.Entry(site).State = EntityState.Detached;
            throw new ConflictException(SiteRules.NameTaken(site.Name));
        }
    }

    private async Task SaveRoomAsync(Room room, CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(RoomConfiguration.SiteAndNameIndex))
        {
            context.Entry(room).State = EntityState.Detached;
            var siteName = await context.Sites.Where(s => s.Id == room.SiteId).Select(s => s.Name).FirstOrDefaultAsync(ct);
            throw new ConflictException(SiteRules.RoomNameTaken(siteName ?? "That site", room.Name));
        }
    }
}
