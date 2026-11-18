namespace MedApp.Application.Pathways.Common;

/// <summary>A pathway's title and its patient's name, as the repository reads them for a label.</summary>
public sealed record PathwayLabel(Guid Id, string Title, string PatientFirstName, string PatientLastName);
