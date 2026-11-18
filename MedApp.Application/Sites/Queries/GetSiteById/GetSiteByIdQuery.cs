using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Queries.GetSiteById;

/// <summary>One site with all its rooms, archived ones too; a 404 if there's none.</summary>
public sealed record GetSiteByIdQuery(Guid SiteId) : IRequest<SiteResponse>;
