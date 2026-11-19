using MedApp.Application.Common.Teams;
using MedApp.Application.Attachments.Common;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachmentThumbnail;

/// <summary>
/// An image's thumbnail (made and re-encoded when it was uploaded). Not audited: lists show them every time they're
/// opened, so only downloads of the file itself are. None for a deleted file. Behind the viewer's team wall
/// (<paramref name="Scope"/>; null only outside a request, which is unscoped) a 404, recorded.
/// </summary>
public sealed record GetAttachmentThumbnailQuery(Guid Id, TeamScope? Scope = null) : IRequest<AttachmentDownload>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
