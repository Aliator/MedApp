using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;

/// <summary>
/// A procedure in full, for <paramref name="ViewerId"/> (the signed-in user: their own notes are the ones they can edit)
/// and whether they're an admin (who delete notes; anyone else asks them to).
/// </summary>
public sealed record GetPatientProcedureByIdQuery(Guid Id, Guid? ViewerId = null, bool ViewerIsAdmin = false) : IRequest<PatientProcedureResponse>;
