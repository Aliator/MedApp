using MediatR;

namespace MedApp.Application.Sites.Commands.DeleteSite;

/// <summary>
/// Deletes a site nothing names (no rooms, no closed days); one that's used is archived instead (409). The last site
/// that isn't archived can't be deleted (409).
/// </summary>
public sealed record DeleteSiteCommand(Guid SiteId) : IRequest;
