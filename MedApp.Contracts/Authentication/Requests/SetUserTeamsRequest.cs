namespace MedApp.Contracts.Authentication.Requests;

/// <summary>
/// The teams a member of staff should belong to, exactly, each with their role in it: any others are removed. Empty
/// removes them all.
/// </summary>
public sealed record SetUserTeamsRequest(
    IReadOnlyList<TeamMembershipRequest>? Teams);

/// <summary>A team someone belongs to, and their role in it: <c>Member</c> (the default) or <c>ClinicAdmin</c>.</summary>
public sealed record TeamMembershipRequest(
    Guid TeamId,
    string? Role);
