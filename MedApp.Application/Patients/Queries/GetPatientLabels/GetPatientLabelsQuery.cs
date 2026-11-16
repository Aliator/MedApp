using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Queries.GetPatientLabels;

/// <summary>
/// The names of these patients, for the side nav's pins: labels for records the user already chose, not a read of
/// them, so nothing is recorded in the activity log. Patients that no longer exist are left out.
/// </summary>
public sealed record GetPatientLabelsQuery(IReadOnlyList<Guid> Ids) : IRequest<IReadOnlyList<PatientLabelResponse>>;
