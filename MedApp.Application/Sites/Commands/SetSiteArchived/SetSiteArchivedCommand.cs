using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.SetSiteArchived;

/// <summary>
/// Archives a site (it can't be chosen for anything new; what names it keeps it) or restores it. A site with rooms
/// that aren't archived can't be archived, nor can the last site that isn't (409).
/// </summary>
public sealed record SetSiteArchivedCommand(Guid SiteId, bool IsArchived) : IRequest<SiteResponse>;
