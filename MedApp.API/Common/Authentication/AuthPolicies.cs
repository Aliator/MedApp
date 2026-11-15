namespace MedApp.API.Common.Authentication;

public static class AuthPolicies
{
    /// <summary>
    /// Any staff member: role User or Admin. Staff can do almost everything; admin-only endpoints (users, templates,
    /// stage definitions, assigning, unlocking stages, deleting procedures, the audit log) use <c>Roles = "Admin"</c>.
    /// </summary>
    public const string Staff = "Staff";
}
