using System.Reflection;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.API.Controllers;
using MedApp.API.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.UnitTests.Api.Authorisation;

/// <summary>
/// Every endpoint says who may call it with a permission (decision 68), never a role name: each controller action has a
/// <see cref="RequirePermissionAttribute"/> (its own or its controller's), or is <c>[AllowAnonymous]</c>, or is one of
/// the few plain <c>[Authorize]</c> ones below, open to anyone signed in whatever their roles.
/// </summary>
[TestFixture]
public sealed class AuthorisationConventionTests
{
    /// <summary>
    /// Open to anyone signed in, with or without a role: the session itself (its expiry, staying signed in), the
    /// notepad (the user's own), and confirming an email address (which every account hold allows).
    /// </summary>
    private static readonly HashSet<string> PlainAuthorize =
    [
        $"{nameof(AuthController)}.{nameof(AuthController.GetSession)}",
        $"{nameof(AuthController)}.{nameof(AuthController.ExtendSession)}",
        $"{nameof(AccountSecurityController)}.{nameof(AccountSecurityController.ConfirmEmail)}",
        $"{nameof(NotepadController)}.{nameof(NotepadController.GetNotepad)}",
        $"{nameof(NotepadController)}.{nameof(NotepadController.SaveNotepad)}",
    ];

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions() =>
        typeof(AuthController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => (t, m)));

    private static IReadOnlyList<AuthorizeAttribute> AuthorizeOn(Type controller, MethodInfo action) =>
        controller.GetCustomAttributes<AuthorizeAttribute>().Concat(action.GetCustomAttributes<AuthorizeAttribute>()).ToList();

    [Test]
    public void EveryAction_NamesAPermission_OrIsAnonymous_OrIsAllowedPlainAuthorize()
    {
        var unguarded = new List<string>();
        foreach (var (controller, action) in Actions())
        {
            var name = $"{controller.Name}.{action.Name}";
            var attributes = AuthorizeOn(controller, action);

            if (attributes.OfType<RequirePermissionAttribute>().Any())
                continue;
            if (action.GetCustomAttributes<AllowAnonymousAttribute>().Any())
                continue;
            if (PlainAuthorize.Contains(name)
                && attributes.Count > 0
                && attributes.All(a => a.Policy is null && a.Roles is null))
                continue;

            unguarded.Add(name);
        }

        unguarded.Should().BeEmpty("every endpoint names its permission (or is deliberately anonymous or open to anyone signed in)");
    }

    [Test]
    public void PlainAuthorizeList_HasOnlyRealActions()
    {
        var names = Actions().Select(a => $"{a.Controller.Name}.{a.Action.Name}").ToHashSet();

        PlainAuthorize.Should().BeSubsetOf(names);
    }

    [Test]
    public void NoAuthorizeAttribute_NamesARoleOrTheOldStaffPolicy()
    {
        var byRole = Actions()
            .SelectMany(a => AuthorizeOn(a.Controller, a.Action).Select(attribute => (Name: $"{a.Controller.Name}.{a.Action.Name}", attribute)))
            .Concat(typeof(NotificationsHub).GetCustomAttributes<AuthorizeAttribute>().Select(attribute => (Name: nameof(NotificationsHub), attribute)))
            .Where(x => x.attribute.Roles is not null || x.attribute.Policy == "Staff")
            .Select(x => x.Name)
            .ToList();

        byRole.Should().BeEmpty();
    }

    [Test]
    public void TheNotificationsHub_NeedsUseApp()
    {
        typeof(NotificationsHub).GetCustomAttributes<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .Should().Equal(MedApp.Application.Common.Identity.Permission.UseApp);
    }

    /// <summary>
    /// The API's code never asks for a role by name: no <c>Roles =</c>, <c>AuthPolicies.Staff</c> or
    /// <c>User.IsInRole</c>. Handlers get permissions instead (decision 51).
    /// </summary>
    [Test]
    public void ApiSource_NeverChecksARoleByName()
    {
        var api = Path.Combine(RepositoryRoot(), "MedApp.API");
        var offenders = Directory.EnumerateFiles(api, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, i) => (path, line, i))
                .Where(x => !x.line.TrimStart().StartsWith("//")
                            && (x.line.Contains("IsInRole(") || x.line.Contains("Roles =") || x.line.Contains("AuthPolicies.Staff"))))
            .Select(x => $"{Path.GetRelativePath(api, x.path)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();

        offenders.Should().BeEmpty();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MedApp.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (MedApp.sln) wasn't found.");
    }
}
