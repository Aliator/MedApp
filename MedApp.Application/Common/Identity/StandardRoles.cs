using static MedApp.Application.Common.Identity.Permission;

namespace MedApp.Application.Common.Identity;

/// <summary>A role the app starts with, and the permissions it starts with (<see cref="StandardRoles"/>).</summary>
public sealed record StandardRole(Guid Id, string Name, string Description, IReadOnlySet<Permission> Permissions);

/// <summary>
/// The roles every database has: the built-in <c>Admin</c>; "All staff (before roles)", which was the old <c>User</c>
/// role, with exactly what staff could do before roles; and the starting roles (decision 68's table, with Senior
/// clinician from decision 138 of the Phases 3 to 5 plan). The migration <c>AddedRolePermissions</c> writes them, and
/// later ones change them (<c>AddedWorkingHours</c>, <c>SplitOverrideStages</c>, <c>AddedMdtItems</c>); the integration tests and the demo
/// data put them back as they were. Every one but Admin stays editable.
/// <para>
/// The IDs are fixed for the starting roles and for Admin and All staff where the migration made them; on a database
/// that already had <c>Admin</c> and <c>User</c>, those keep their own IDs, so look them up by name.
/// </para>
/// </summary>
public static class StandardRoles
{
    public static readonly StandardRole Admin = new(
        Guid.Parse("7a3b0000-0000-4000-8000-000000000001"),
        AppRoles.Admin,
        "Runs MedApp: accounts, roles, setup and privacy. Built in, with fixed permissions.",
        Permissions.Admin);

    /// <summary>
    /// The old <c>User</c> role, renamed: exactly what every member of staff could do before roles, so nobody lost
    /// anything when roles came. Admins move people onto the starting roles, then archive it.
    /// </summary>
    public static readonly StandardRole AllStaffBeforeRoles = new(
        Guid.Parse("7a3b0000-0000-4000-8000-000000000002"),
        AppRoles.AllStaffBeforeRoles,
        "What every member of staff could do before roles. Move people onto the starting roles, then archive it.",
        new HashSet<Permission>
        {
            UseApp, ViewPatients, EditPatients, DeletePatients, RegisterReferrals, CreatePathways, WorkPathways,
            BookAppointments, AttachFiles,
        });

    public static readonly StandardRole Clinician = new(
        Guid.Parse("7a3b0000-0000-4000-8000-000000000003"),
        "Clinician",
        "Clinicians who triage referrals and lead pathways.",
        new HashSet<Permission>
        {
            UseApp, ViewPatients, EditPatients, RegisterReferrals, TriageReferrals, CreatePathways, WorkPathways,
            UnlockStages, FinishWithoutVisit, RecordMdtOutcomes, BookAppointments, AttachFiles, RecordSpecimens, SignOffResults, WriteLetters, ApproveLetters, SendLetters,
        });

    /// <summary>
    /// Clinician's permissions, with both stage overrides (decision 138 of <c>docs/centre-phases-3-5-plan.md</c>; the
    /// migration <c>SplitOverrideStages</c> adds it). Clinician keeps both too, so nobody lost anything when
    /// <c>OverrideStages</c> split; an organisation whose senior clinicians alone should unlock stages and finish one
    /// without its visit gives them this role and takes the two off Clinician.
    /// </summary>
    public static readonly StandardRole SeniorClinician = new(
        Guid.Parse("7a3b0000-0000-4000-8000-000000000008"),
        "Senior clinician",
        "Senior clinicians: as Clinician, and they unlock stages and finish one without its visit.",
        new HashSet<Permission>
        {
            UseApp, ViewPatients, EditPatients, RegisterReferrals, TriageReferrals, CreatePathways, WorkPathways,
            UnlockStages, FinishWithoutVisit, RecordMdtOutcomes, BookAppointments, AttachFiles, RecordSpecimens, SignOffResults, WriteLetters, ApproveLetters, SendLetters,
        });

    public static readonly StandardRole Nurse = new(
        Guid.Parse("7a3b0000-0000-4000-8000-000000000004"),
        "Nurse",
        "Nurses and healthcare assistants who work on pathways.",
        new HashSet<Permission>
        {
            UseApp, ViewPatients, EditPatients, RegisterReferrals, CreatePathways, WorkPathways, BookAppointments,
            AttachFiles, RecordSpecimens, WriteLetters, SendLetters,
        });

    public static readonly StandardRole Booking = new(
        Guid.Parse("7a3b0000-0000-4000-8000-000000000005"),
        "Booking",
        "The booking team: patients, referrals, appointments and calendars.",
        new HashSet<Permission>
        {
            UseApp, ViewPatients, EditPatients, DeletePatients, RegisterReferrals, BookAppointments, ManageAnyCalendar,
            ManageClinics, AttachFiles, SendLetters,
        });

    public static readonly StandardRole Lab = new(
        Guid.Parse("7a3b0000-0000-4000-8000-000000000006"),
        "Lab",
        "Laboratory staff who work on their pathway stages.",
        new HashSet<Permission> { UseApp, ViewPatients, WorkPathways, AttachFiles, RecordSpecimens, ProcessSpecimens });

    public static readonly StandardRole Manager = new(
        Guid.Parse("7a3b0000-0000-4000-8000-000000000007"),
        "Manager",
        "Service managers: assigning work, meetings, Insights and monitoring.",
        new HashSet<Permission>
        {
            UseApp, ViewPatients, AssignPathways, ArrangeMeetings, ManageClinics, ViewInsights, ManageMonitoring,
        });

    /// <summary>The six starting roles, editable, in the order the page lists them.</summary>
    public static readonly IReadOnlyList<StandardRole> Starting = [Clinician, SeniorClinician, Nurse, Booking, Lab, Manager];

    /// <summary>Every standard role: Admin, All staff (before roles), then the starting roles.</summary>
    public static readonly IReadOnlyList<StandardRole> All = [Admin, AllStaffBeforeRoles, .. Starting];
}
