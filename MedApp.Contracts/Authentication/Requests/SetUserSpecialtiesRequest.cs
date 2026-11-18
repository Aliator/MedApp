namespace MedApp.Contracts.Authentication.Requests;

/// <summary>The specialties a member of staff should belong to, exactly: any others are removed. Empty removes them all.</summary>
public sealed record SetUserSpecialtiesRequest(
    IReadOnlyList<Guid>? SpecialtyIds);
