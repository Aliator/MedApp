namespace MedApp.Application.PatientMessages.Common;

/// <summary>Where an appointment is, as a message names it: the site of its room (the organisation's details, never a person's).</summary>
public sealed record MessageSite(string Name, IReadOnlyList<string> AddressLines, string? Postcode, string? Phone);
