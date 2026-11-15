using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.Audit.Responses;
using MediatR;

namespace MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;

/// <summary>
/// One page of the procedure audit log (admins only). See <see cref="ProcedureAuditSearch"/> for the filters;
/// <paramref name="Actions"/> is any of several.
/// </summary>
public sealed record SearchProcedureAuditQuery(
    int Page,
    int PageSize,
    bool Ascending,
    Guid? ProcedureId,
    Guid? PatientId,
    Guid? UserId,
    string? Action,
    string? Search,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<string>? Actions = null)
    : IRequest<PagedResponse<PatientProcedureAuditEntryResponse>>;
