namespace MedApp.Contracts.Patients.Responses;

/// <summary>A patient's name only, for labelling something the user already chose (a pin in the side nav).</summary>
public sealed record PatientLabelResponse(Guid Id, string Name);
