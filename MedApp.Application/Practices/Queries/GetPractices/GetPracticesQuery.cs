using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Queries.GetPractices;

/// <summary>The directory, sorted by name, with what has each practice; archived ones only when asked for.</summary>
public sealed record GetPracticesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<PracticeResponse>>;
