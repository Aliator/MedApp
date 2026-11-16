using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypes;

/// <summary>Every activity type, sorted by name; archived ones only when asked for.</summary>
public sealed record GetActivityTypesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<ActivityTypeResponse>>;
