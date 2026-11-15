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
/// <c>FirstAdmin</c> settings. It adds the Admin and User roles if they're missing, then the admin, but only while
/// there are no accounts at all. So it's safe to run on every deploy: once anyone exists, it changes nothing.
/// </summary>
public sealed class FirstAdminSetup(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IIdentityTransactions transactions,
    TimeProvider timeProvider,
    ILogger<FirstAdminSetup> logger)
{
    public const string Command = "create-first-admin";
    public const string Section = "FirstAdmin";
    public const string AdminRole = "Admin";
    public static readonly IReadOnlyList<string> Roles = [AdminRole, "User"];

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
            foreach (var role in Roles)
            {
                if (await roleManager.RoleExistsAsync(role))
                    continue;

                var created = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (!created.Succeeded)
                    return created;
            }

            if (await userManager.Users.AnyAsync(ct))
            {
                accountsExist = true;
                return IdentityResult.Success;
            }

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
}

public enum FirstAdminOutcome
{
    Created,
    AccountsExist,
    NotConfigured,
    Failed,
}

public sealed record FirstAdminResult(FirstAdminOutcome Outcome, IReadOnlyList<IdentityError> Errors);
