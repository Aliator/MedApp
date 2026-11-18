using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Makes the first admin on a new database, deliberately: a deploy runs the API's <c>create-first-admin</c> command
/// (the Helm chart's migrations Job does, after migrating), with the username and password from a secret, in the
/// <c>FirstAdmin</c> settings. It adds the built-in Admin role if it's missing (the migration <c>AddedRolePermissions</c>
/// makes it), then the admin, with Admin alone (whose permissions include <see cref="Permission.UseApp"/>, so they can
/// change their own password), but only while there are no accounts at all. So it's safe to run on every deploy: once
/// anyone exists, it changes nothing.
/// </summary>
public sealed class FirstAdminSetup(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IIdentityTransactions transactions,
    TimeProvider timeProvider,
    ILogger<FirstAdminSetup> logger)
{
    public const string Command = "create-first-admin";
    public const string Section = "FirstAdmin";
    public const string AdminRole = AppRoles.Admin;

    /// <summary>
    /// The command: reads the <c>FirstAdmin</c> settings and runs the setup. Returns the process's exit code:
    /// 0 when the admin was made or wasn't needed, 1 when it couldn't be (no settings, or Identity refused).
    /// </summary>
    public static async Task<int> RunCommandAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetSection(Section);
        var setup = scope.ServiceProvider.GetRequiredService<FirstAdminSetup>();

        var result = await setup.RunAsync(settings["Username"], settings["Password"], ct);
        return result.Outcome is FirstAdminOutcome.Created or FirstAdminOutcome.AccountsExist ? 0 : 1;
    }

    public async Task<FirstAdminResult> RunAsync(string? username, string? password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            logger.LogError("No first admin made: {Section}:Username and {Section}:Password must both be set.", Section, Section);
            return new FirstAdminResult(FirstAdminOutcome.NotConfigured, []);
        }

        var accountsExist = false;
        var result = await transactions.RunAsync(async () =>
        {
            if (!await roleManager.RoleExistsAsync(AdminRole))
            {
                var created = await roleManager.CreateAsync(NewAdminRole(timeProvider.GetUtcNow().UtcDateTime));
                if (!created.Succeeded)
                    return created;
            }

            if (await userManager.Users.AnyAsync(ct))
            {
                accountsExist = true;
                return IdentityResult.Success;
            }

            // Not made to change its password (MustChangePassword stays false), unlike accounts an admin creates: this
            // password isn't handed on by an admin, but comes from the secret store, set by whoever deploys. On kind,
            // up.sh generates it and it can be read back from the cluster's store, which is acceptable for a local
            // cluster.
            var admin = new ApplicationUser
            {
                UserName = username,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            };
            var made = await userManager.CreateAsync(admin, password);
            return made.Succeeded ? await userManager.AddToRoleAsync(admin, AdminRole) : made;
        }, ct);

        if (!result.Succeeded)
        {
            logger.LogError(
                "No first admin made: {Errors}",
                string.Join(" ", result.Errors.Select(e => e.Description)));
            return new FirstAdminResult(FirstAdminOutcome.Failed, result.Errors.ToList());
        }

        if (accountsExist)
        {
            logger.LogInformation("No first admin needed: the database already has accounts.");
            return new FirstAdminResult(FirstAdminOutcome.AccountsExist, []);
        }

        logger.LogInformation("Made the first admin, {Username}.", username);
        return new FirstAdminResult(FirstAdminOutcome.Created, []);
    }

    /// <summary>The built-in Admin role, as the migration makes it on a new database.</summary>
    public static ApplicationRole NewAdminRole(DateTime nowUtc) => new(AdminRole)
    {
        Id = StandardRoles.Admin.Id,
        Description = StandardRoles.Admin.Description,
        IsBuiltIn = true,
        CreatedAt = nowUtc,
        LastUpdated = nowUtc,
    };
}

public enum FirstAdminOutcome
{
    Created,
    AccountsExist,
    NotConfigured,
    Failed,
}

public sealed record FirstAdminResult(FirstAdminOutcome Outcome, IReadOnlyList<IdentityError> Errors);
