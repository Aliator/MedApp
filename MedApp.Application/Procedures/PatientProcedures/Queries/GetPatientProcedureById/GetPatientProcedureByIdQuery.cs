using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;

/// <summary>A procedure in full, for <paramref name="ViewerId"/> (the signed-in user: their own notes are the ones they can edit).</summary>
public sealed record GetPatientProcedureByIdQuery(Guid Id, Guid? ViewerId = null) : IRequest<PatientProcedureResponse>;
