using MedApp.Application.Attachments.Common;
using MedApp.Application.Procedures.Common;
using MediatR;

namespace MedApp.Application.Attachments.Commands.DownloadAttachment;

/// <summary>
/// A file's bytes, to download. A command, not a query: every download is audited (in the audit log, not on the
/// timeline). Deleted files can't be downloaded, by anyone.
/// </summary>
public sealed record DownloadAttachmentCommand(Guid Id, ProcedureActor Actor) : IRequest<AttachmentDownload>;
