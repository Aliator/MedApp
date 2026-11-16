using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MediatR;

namespace MedApp.Application.Activity.Queries.SearchActivity;

/// <summary>
/// One page of the activity log (admins only). See <see cref="ActivitySearch"/> for the filters; <paramref name="Actions"/>
/// is any of several.
/// </summary>
public sealed record SearchActivityQuery(
    int Page,
    int PageSize,
    bool Ascending,
    Guid? PatientId,
    Guid? UserId,
    string? Search,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<string>? Actions = null)
    : IRequest<PagedResponse<ActivityEntryResponse>>;
