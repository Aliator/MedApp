using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.Audit.Responses;
using MediatR;

namespace MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;

/// <summary>
/// One page of the pathway audit log (admins only). See <see cref="PathwayAuditSearch"/> for the filters;
/// <paramref name="Actions"/> is any of several.
/// </summary>
public sealed record SearchPathwayAuditQuery(
    int Page,
    int PageSize,
    bool Ascending,
    Guid? PathwayId,
    Guid? PatientId,
    Guid? UserId,
    string? Action,
    string? Search,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<string>? Actions = null)
    : IRequest<PagedResponse<PatientPathwayAuditEntryResponse>>;
