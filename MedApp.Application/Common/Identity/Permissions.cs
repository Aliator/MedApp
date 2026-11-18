namespace MedApp.Application.Common.Identity;

/// <summary>
/// What a member of staff may do. Fixed in code: a permission is only real if code checks it, so only code adds one.
/// Roles (data, which admins manage) grant them, and a person's permissions are those of all their roles together.
/// <para>
/// <b>Stored by name</b> (<c>RolePermissions.Permission</c>), never by number, so the list can be reordered freely.
/// Renaming one needs a migration that renames its rows; a stored name that code no longer knows grants nothing.
/// </para>
/// </summary>
public enum Permission
{
    /// <summary>The base every role grants (never a tick): the person's own account, calendar and setup lists.</summary>
    UseApp,

    ViewPatients,
    EditPatients,
    DeletePatients,

    RegisterReferrals,
    TriageReferrals,

    CreatePathways,
    WorkPathways,
    AssignPathways,
    OverrideStages,
    DeletePathways,

    BookAppointments,
    ManageAnyCalendar,
    ArrangeMeetings,
    ManageClinics,

    AttachFiles,

    WriteLetters,
    ApproveLetters,
    SendLetters,

    ViewInsights,
    ManageMonitoring,

    ManageSetup,

    ManageUsers,
    ManageRoles,
    ManageSecuritySettings,
    ViewAudit,
    ExportPatientRecords,
    ManagePrivacy,
    CorrectRecords,
}

/// <summary>The groups the roles page shows permissions in.</summary>
public enum PermissionGroup
{
    /// <summary><see cref="Permission.UseApp"/> alone: every role has it, so it isn't shown as a tick.</summary>
    Base,
    Patients,
    Referrals,
    Pathways,
    AppointmentsAndCalendars,
    Files,
    Letters,
    Oversight,
    Setup,

    /// <summary>Only the built-in Admin role has these; any other role is refused them.</summary>
    AdministratorOnly,
}

/// <summary>A permission with its group and a plain description of what it allows.</summary>
public sealed record PermissionDescription(Permission Permission, PermissionGroup Group, string Description)
{
    public string Name => Permission.ToString();

    /// <summary>Only the built-in Admin role may have it (<see cref="PermissionGroup.AdministratorOnly"/>).</summary>
    public bool IsAdminOnly => Group == PermissionGroup.AdministratorOnly;
}

/// <summary>
/// The permission catalogue (each permission's group and description), the built-in Admin role's fixed set, and how a
/// person's roles add up to their permissions. See <c>docs/centre-phase2-plan.md</c>, decisions 51 to 68.
/// </summary>
public static class Permissions
{
    public static readonly IReadOnlyList<PermissionDescription> Catalogue =
    [
        new(Permission.UseApp, PermissionGroup.Base,
            "Use the app: your own account, settings, notifications, notepad and calendar; read calendars, meetings and the setup lists."),

        new(Permission.ViewPatients, PermissionGroup.Patients,
            "See patients, their timeline, pathways, appointments, files and referrals."),
        new(Permission.EditPatients, PermissionGroup.Patients,
            "Add and edit patients, and look them up in the demographics service."),
        new(Permission.DeletePatients, PermissionGroup.Patients,
            "Delete a patient who has nothing attached."),

        new(Permission.RegisterReferrals, PermissionGroup.Referrals,
            "Register, edit and withdraw referrals, and mark them entered in error."),
        new(Permission.TriageReferrals, PermissionGroup.Referrals,
            "Accept, reject or redirect referrals in your specialties."),

        new(Permission.CreatePathways, PermissionGroup.Pathways,
            "Start a pathway for a patient."),
        new(Permission.WorkPathways, PermissionGroup.Pathways,
            "Work on pathways: stages, notes, details, hold, discharge and cancel; ask for a note to be deleted."),
        new(Permission.AssignPathways, PermissionGroup.Pathways,
            "Assign staff to pathways, and unassign them."),
        new(Permission.OverrideStages, PermissionGroup.Pathways,
            "Unlock a finished stage, and finish a stage that requires a visit without one."),
        new(Permission.DeletePathways, PermissionGroup.Pathways,
            "Delete a cancelled or archived pathway."),

        new(Permission.BookAppointments, PermissionGroup.AppointmentsAndCalendars,
            "Request, book, move and cancel appointments, record outcomes and notes, and record a visit after the event."),
        new(Permission.ManageAnyCalendar, PermissionGroup.AppointmentsAndCalendars,
            "Change other people's calendars: their blocks, day and week templates and default week."),
        new(Permission.ArrangeMeetings, PermissionGroup.AppointmentsAndCalendars,
            "Arrange, change and cancel meetings; see the team view and suggested times."),
        new(Permission.ManageClinics, PermissionGroup.AppointmentsAndCalendars,
            "Set staff working hours."),

        new(Permission.AttachFiles, PermissionGroup.Files,
            "Attach files, and delete your own."),

        new(Permission.WriteLetters, PermissionGroup.Letters,
            "Write letters about patients from templates, and submit them for approval."),
        new(Permission.ApproveLetters, PermissionGroup.Letters,
            "Approve or return letters submitted to you, and approve your own."),
        new(Permission.SendLetters, PermissionGroup.Letters,
            "Send approved letters by secure email, or record them as posted or handed over."),

        new(Permission.ViewInsights, PermissionGroup.Oversight,
            "See Insights."),
        new(Permission.ManageMonitoring, PermissionGroup.Oversight,
            "Set up your own monitoring rules."),

        new(Permission.ManageSetup, PermissionGroup.Setup,
            "Manage the stage library, pathway templates, specialties, activity types and closed days."),

        new(Permission.ManageUsers, PermissionGroup.AdministratorOnly,
            "Manage accounts: create and delete them, give roles, reset passwords and two-factor, sign people out everywhere."),
        new(Permission.ManageRoles, PermissionGroup.AdministratorOnly,
            "Manage roles and their permissions."),
        new(Permission.ManageSecuritySettings, PermissionGroup.AdministratorOnly,
            "Change the security settings."),
        new(Permission.ViewAudit, PermissionGroup.AdministratorOnly,
            "See the pathway audit log, the activity log and the timeline's full view."),
        new(Permission.ExportPatientRecords, PermissionGroup.AdministratorOnly,
            "Export everything held about a patient."),
        new(Permission.ManagePrivacy, PermissionGroup.AdministratorOnly,
            "Restrict, dispute and anonymise records, and review and keep them past retention."),
        new(Permission.CorrectRecords, PermissionGroup.AdministratorOnly,
            "Delete notes and decide deletion requests, delete anyone's file, and withdraw anyone's visit recorded after the event."),
    ];

    private static readonly Dictionary<Permission, PermissionDescription> ByPermission =
        Catalogue.ToDictionary(p => p.Permission);

    private static readonly Dictionary<string, Permission> ByName =
        Catalogue.ToDictionary(p => p.Name, p => p.Permission, StringComparer.Ordinal);

    /// <summary>
    /// The clinical permissions the built-in Admin role doesn't get (<c>docs/centre-phases-3-5-plan.md</c>, decision 74):
    /// triage, and writing, approving and sending letters. An admin who does that work holds a role that grants it too.
    /// </summary>
    public static readonly IReadOnlySet<Permission> NotForAdmin = new HashSet<Permission>
    {
        Permission.TriageReferrals,
        Permission.WriteLetters,
        Permission.ApproveLetters,
        Permission.SendLetters,
    };

    /// <summary>
    /// The built-in Admin role's permissions, fixed in code: every one today's admins use, which is all of them but the
    /// clinical ones (<see cref="NotForAdmin"/>: triage, and letters).
    /// </summary>
    public static readonly IReadOnlySet<Permission> Admin =
        Enum.GetValues<Permission>().Where(p => !NotForAdmin.Contains(p)).ToHashSet();

    /// <summary>A permission's group and description.</summary>
    public static PermissionDescription Describe(Permission permission) => ByPermission[permission];

    /// <summary>
    /// The permission with exactly this stored name (case and all), or false for a name code doesn't know (renamed or
    /// removed since), which grants nothing. Numbers aren't names: "1" is unknown.
    /// </summary>
    public static bool TryParse(string? name, out Permission permission)
    {
        permission = default;
        return name is not null && ByName.TryGetValue(name, out permission);
    }

    /// <summary>
    /// What one role grants: the built-in Admin role its fixed set (<see cref="Admin"/>); any other role its stored
    /// permissions that code knows and that aren't for Admin only (so a row written around the roles page grants
    /// nothing it couldn't), plus <see cref="Permission.UseApp"/>, which every role grants.
    /// </summary>
    public static IReadOnlySet<Permission> GrantedBy(bool isBuiltInAdmin, IEnumerable<string?> storedNames)
    {
        if (isBuiltInAdmin)
            return Admin;

        var granted = new HashSet<Permission> { Permission.UseApp };
        foreach (var name in storedNames)
            if (TryParse(name, out var permission) && !Describe(permission).IsAdminOnly)
                granted.Add(permission);
        return granted;
    }

    /// <summary>A person's permissions: those of all their roles together. No role, no permissions.</summary>
    public static IReadOnlySet<Permission> GrantedBy(IEnumerable<IReadOnlySet<Permission>> roles) =>
        roles.SelectMany(role => role).ToHashSet();
}
