using MedApp.Application.Attachments.Common;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachmentThumbnail;

/// <summary>
/// An image's thumbnail (made and re-encoded when it was uploaded). Not audited: lists show them every time they're
/// opened, so only downloads of the file itself are. None for a deleted file.
/// </summary>
public sealed record GetAttachmentThumbnailQuery(Guid Id) : IRequest<AttachmentDownload>;
