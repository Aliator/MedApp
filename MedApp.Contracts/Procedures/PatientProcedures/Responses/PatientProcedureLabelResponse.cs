namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

/// <summary>A procedure's title and its patient's name only, for labelling something the user already chose (a pin in the side nav).</summary>
public sealed record PatientProcedureLabelResponse(Guid Id, string Title, string PatientName);
