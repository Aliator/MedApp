using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Recovery when every admin is locked out: whoever deploys runs the API's <c>grant-admin &lt;username&gt;</c> command
/// (as <c>create-first-admin</c>), which gives that existing account the built-in Admin role. It needs the same access
/// as the first admin's secret (a shell in the deployment), so it adds no new way in. It's logged as a security event
/// (<see cref="SecurityEventKind.AdminGrantedByCommand"/>: the activity log's <c>RolesChanged</c>, by nobody, with the
/// account as its target) and at Warning in the application log.
/// </summary>
public sealed class AdminGrant(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IIdentityTransactions transactions,
    ISecurityEventLog securityEvents,
    TimeProvider timeProvider,
    ILogger<AdminGrant> logger)
{
    public const string Command = "grant-admin";

    /// <summary>The command: 0 when the account is an admin afterwards (or was already), 1 when it couldn't be made one.</summary>
    public static async Task<int> RunCommandAsync(IServiceProvider services, string username, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var grant = scope.ServiceProvider.GetRequiredService<AdminGrant>();
        var outcome = await grant.RunAsync(username, ct);
        return outcome is AdminGrantOutcome.Granted or AdminGrantOutcome.AlreadyAdmin ? 0 : 1;
    }

    public async Task<AdminGrantOutcome> RunAsync(string? username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            logger.LogError("No admin granted: give the account's username, as {Command} <username>.", Command);
            return AdminGrantOutcome.NoUsername;
        }

        ApplicationUser? user = null;
        var alreadyAdmin = false;
        var result = await transactions.RunAsync(async () =>
        {
            user = await userManager.FindByNameAsync(username);
            if (user is null)
                return IdentityResult.Failed(IdentityErrors.UserNotFound);

            if (!await roleManager.RoleExistsAsync(AppRoles.Admin))
            {
                var created = await roleManager.CreateAsync(FirstAdminSetup.NewAdminRole(timeProvider.GetUtcNow().UtcDateTime));
                if (!created.Succeeded)
                    return created;
            }

            if (await userManager.IsInRoleAsync(user, AppRoles.Admin))
            {
                alreadyAdmin = true;
                return IdentityResult.Success;
            }

            var added = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!added.Succeeded)
                return added;

            // In the same transaction as the grant: kept or lost together.
            await securityEvents.RecordAsync(
                new SecurityEvent(
                    SecurityEventKind.AdminGrantedByCommand,
                    user.Id,
                    user.UserName,
                    Details: new Dictionary<string, object?> { ["added"] = AppRoles.Admin, ["via"] = Command }),
                ct);
            return added;
        }, ct);

        if (!result.Succeeded)
        {
            var notFound = result.Errors.Any(e => e.Code == IdentityErrors.UserNotFoundCode);
            logger.LogError(
                "No admin granted: {Errors}",
                notFound ? "no account has that username." : string.Join(" ", result.Errors.Select(e => e.Description)));
            return notFound ? AdminGrantOutcome.UserNotFound : AdminGrantOutcome.Failed;
        }

        if (alreadyAdmin)
        {
            logger.LogInformation("No admin granted: {Username} is already an admin.", user!.UserName);
            return AdminGrantOutcome.AlreadyAdmin;
        }

        logger.LogWarning("Gave {Username} the Admin role with the {Command} command.", user!.UserName, Command);
        return AdminGrantOutcome.Granted;
    }
}

public enum AdminGrantOutcome
{
    Granted,
    AlreadyAdmin,
    NoUsername,
    UserNotFound,
    Failed,
}
