namespace MedApp.Application.Common.Identity;

/// <summary>
/// Keeps one active admin (an account with the built-in Admin role that isn't locked out) at all times: taking Admin
/// from the last one, or deleting it, is refused (<see cref="IdentityErrors.LastAdmin"/>, a 409). If every admin is
/// locked out anyway, whoever deploys runs <c>grant-admin &lt;username&gt;</c>.
/// </summary>
public interface ILastAdminGuard
{
    /// <summary>
    /// Whether taking the Admin role from this user, or deleting them, would leave no active admin. It must be called
    /// inside the change's transaction (on SQL Server it throws otherwise): it holds a lock until the transaction ends,
    /// so two admins removing each other at once can't both succeed.
    /// </summary>
    Task<bool> WouldLeaveNoAdminAsync(Guid userId, CancellationToken ct);
}
