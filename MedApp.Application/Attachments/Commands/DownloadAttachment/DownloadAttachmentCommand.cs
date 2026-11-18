using MedApp.Application.Attachments.Common;
using MedApp.Application.Pathways.Common;
using MediatR;

namespace MedApp.Application.Attachments.Commands.DownloadAttachment;

/// <summary>
/// A file's bytes, to download. A command, not a query: every download is audited (in the pathway audit log, not on
/// the timeline), and recorded in the activity log as a read of the patient's data. Deleted files can't be
/// downloaded, by anyone. Downloading changes nothing, so it's allowed while the record is restricted.
/// </summary>
public sealed record DownloadAttachmentCommand(Guid Id, PathwayActor Actor) : IRequest<AttachmentDownload>;
