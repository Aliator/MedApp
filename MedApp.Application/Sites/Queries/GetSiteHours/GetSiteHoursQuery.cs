using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Queries.GetSiteHours;

/// <summary>A site's opening hours today.</summary>
public sealed record GetSiteHoursQuery(Guid SiteId) : IRequest<SiteHoursResponse>;
