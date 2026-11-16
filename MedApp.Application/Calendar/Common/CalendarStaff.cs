using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Calendar.Common;

public static class CalendarStaff
{
    /// <summary>A member of staff's username, or a 404 if they aren't one (no account, or no role).</summary>
    public static async Task<string> GetUsernameOrThrowAsync(
        this IIdentityReadService identity, Guid staffUserId, CancellationToken ct)
    {
        var staff = await identity.GetStaffAsync(ct);
        return staff
                   .Where(s => s.UserId == staffUserId)
                   .Select(s => (string?)s.Username)
                   .FirstOrDefault()
               ?? throw new NotFoundException($"Staff member '{staffUserId}' was not found.");
    }
}
