using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureLabels;

/// <summary>
/// These procedures' titles and patients' names, for the side nav's pins: labels for records the user already chose,
/// not a read of them, so nothing is recorded in the activity log. Procedures that no longer exist are left out.
/// </summary>
public sealed record GetPatientProcedureLabelsQuery(IReadOnlyList<Guid> Ids) : IRequest<IReadOnlyList<PatientProcedureLabelResponse>>;
