namespace MedApp.Contracts.Pathways.PatientPathways.Responses;

/// <summary>A pathway's title and its patient's name only, for labelling something the user already chose (a pin in the side nav).</summary>
public sealed record PatientPathwayLabelResponse(Guid Id, string Title, string PatientName);
