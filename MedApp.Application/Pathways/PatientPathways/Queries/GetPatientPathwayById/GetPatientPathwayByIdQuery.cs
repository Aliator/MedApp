using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;

/// <summary>
/// A pathway in full, for <paramref name="ViewerId"/> (the signed-in user: their own notes are the ones they can edit)
/// and whether they're an admin (who delete notes; anyone else asks them to).
/// </summary>
public sealed record GetPatientPathwayByIdQuery(Guid Id, Guid? ViewerId = null, bool ViewerIsAdmin = false) : IRequest<PatientPathwayResponse>;
