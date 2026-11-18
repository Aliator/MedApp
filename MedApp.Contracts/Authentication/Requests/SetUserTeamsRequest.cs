namespace MedApp.Contracts.Authentication.Requests;

/// <summary>The teams a member of staff should belong to, exactly: any others are removed. Empty removes them all.</summary>
public sealed record SetUserTeamsRequest(
    IReadOnlyList<Guid>? TeamIds);
