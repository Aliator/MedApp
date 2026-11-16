namespace MedApp.Application.Procedures.Common;

/// <summary>A procedure's title and its patient's name, as the repository reads them for a label.</summary>
public sealed record ProcedureLabel(Guid Id, string Title, string PatientFirstName, string PatientLastName);
