using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Queries.GetSites;

/// <summary>Every site by name, each with its rooms; archived sites and rooms only when asked for.</summary>
public sealed record GetSitesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<SiteResponse>>;
