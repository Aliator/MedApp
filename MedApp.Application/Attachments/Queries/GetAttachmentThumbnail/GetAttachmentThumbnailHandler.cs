using MedApp.Application.Common.Teams;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachmentThumbnail;

public sealed class GetAttachmentThumbnailHandler(IAttachmentRepository attachments, IFileStore fileStore, ITeamBoundary boundary)
    : IRequestHandler<GetAttachmentThumbnailQuery, AttachmentDownload>
{
    public async Task<AttachmentDownload> Handle(GetAttachmentThumbnailQuery request, CancellationToken cancellationToken)
    {
        if (!fileStore.IsConfigured)
            throw new FileStorageUnavailableException();

        var attachment = await attachments.GetByIdAsync(request.Id, cancellationToken);
        var type = attachment is null ? null : AttachmentFileTypes.FromContentType(attachment.ContentType);
        if (attachment is null || attachment.IsDeleted || !attachment.HasThumbnail || type is null)
            throw new NotFoundException($"File '{request.Id}' has no thumbnail.");
        await AttachmentWalls.EnsureVisibleAsync(attachment, boundary, request.Teams, cancellationToken);

        var content = await fileStore.OpenReadAsync(AttachmentStorageKeys.Thumbnail(attachment.Id), cancellationToken)
                      ?? throw new NotFoundException($"File '{request.Id}' has no thumbnail.");

        var contentType = IThumbnailGenerator.ContentTypeOf(type);
        var extension = contentType == "image/png" ? ".png" : ".jpg";
        return new AttachmentDownload($"thumbnail{extension}", contentType, null, content);
    }
}
