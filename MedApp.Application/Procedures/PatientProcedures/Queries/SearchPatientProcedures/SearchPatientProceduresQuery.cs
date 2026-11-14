using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

/// <summary>
/// One page of procedures matching the filters. See <see cref="ProcedureSearch"/> for how they apply;
/// <paramref name="CurrentUserId"/> is the signed-in user, for <paramref name="AssignedToMe"/>. Home's task lists are
/// this search with <paramref name="AssignedToMe"/> and <paramref name="Started"/> set.
/// </summary>
public sealed record SearchPatientProceduresQuery(
    int Page,
    int PageSize,
    string? Sort,
    bool Ascending,
    string? Search,
    Guid? PatientId,
    Guid? TemplateId,
    IReadOnlyList<string>? Statuses,
    string? Priority,
    Guid? AssignedToUserId,
    bool AssignedToMe,
    bool OverdueOnly,
    DateOnly? DueFrom,
    DateOnly? DueTo,
    bool IncludeClosed,
    bool? Started,
    Guid CurrentUserId)
    : IRequest<PagedResponse<PatientProcedureSummaryResponse>>;
