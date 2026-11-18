using MedApp.Application.Common.Identity;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// The permissions each migration after <c>SplitOverrideStages</c> gives the starting roles. A migration test that
/// checks a role's permissions against <see cref="StandardRoles"/> (which has them as they are now) leaves out those
/// added after the migration it stops at, as they aren't there yet. A new migration that grants a permission to the
/// starting roles goes here, or every such test fails once it merges.
/// </summary>
internal static class LaterMigrationPermissions
{
    private static readonly (string Migration, Permission[] Permissions)[] Grants =
    [
        ("20261118174910_AddedLetters", [Permission.WriteLetters, Permission.ApproveLetters, Permission.SendLetters]),
        ("20261118175544_AddedSpecimens", [Permission.RecordSpecimens, Permission.ProcessSpecimens]),
        ("20261118181632_AddedMdtItems", [Permission.RecordMdtOutcomes]),
        ("20261118182523_AddedResults", [Permission.SignOffResults]),
        ("20261118183559_AddedConsentRecords", [Permission.RecordConsent]),
    ];

    /// <summary>The permissions migrations later than this one add (migration IDs sort by their timestamp).</summary>
    public static IReadOnlySet<Permission> AddedAfter(string migration) =>
        Grants.Where(grant => string.CompareOrdinal(grant.Migration, migration) > 0)
            .SelectMany(grant => grant.Permissions)
            .ToHashSet();

    /// <summary>The role's permissions as they are at that migration: now's, less those later migrations add.</summary>
    public static IEnumerable<string> AsAt(StandardRole role, string migration)
    {
        var later = AddedAfter(migration);
        return role.Permissions.Where(p => !later.Contains(p)).Select(p => p.ToString());
    }
}
