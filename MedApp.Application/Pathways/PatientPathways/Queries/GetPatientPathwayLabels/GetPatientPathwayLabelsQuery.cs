using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayLabels;

/// <summary>
/// These pathways' titles and patients' names, for the side nav's pins: labels for records the user already chose,
/// not a read of them, so nothing is recorded in the activity log. Pathways that no longer exist are left out.
/// </summary>
public sealed record GetPatientPathwayLabelsQuery(IReadOnlyList<Guid> Ids) : IRequest<IReadOnlyList<PatientPathwayLabelResponse>>;
