using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;

/// <summary>
/// One page of pathways matching the filters. See <see cref="PathwaySearch"/> for how they apply;
/// <paramref name="CurrentUserId"/> is the signed-in user, for <paramref name="AssignedToMe"/>. Home's task lists are
/// this search with <paramref name="AssignedToMe"/> and <paramref name="Started"/> set. <paramref name="Title"/>,
/// <paramref name="TemplateName"/> and <paramref name="PatientName"/> each match one of what <paramref name="Search"/>
/// matches; <paramref name="Priorities"/> is any of several, as <paramref name="Statuses"/> is.
/// <paramref name="NobodyAssigned"/> keeps only pathways with no one assigned. <paramref name="SpecialtyIds"/> is any of
/// the pathways' own specialties, and <paramref name="StageDefinitionIds"/> the stage reached: any of the definitions
/// their current stage comes from.
/// </summary>
public sealed record SearchPatientPathwaysQuery(
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
    Guid CurrentUserId,
    IReadOnlyList<string>? Priorities = null,
    string? Title = null,
    string? TemplateName = null,
    string? PatientName = null,
    bool NobodyAssigned = false,
    IReadOnlyList<Guid>? SpecialtyIds = null,
    IReadOnlyList<Guid>? StageDefinitionIds = null,
    bool BreachedOnly = false)
    : IRequest<PagedResponse<PatientPathwaySummaryResponse>>;
