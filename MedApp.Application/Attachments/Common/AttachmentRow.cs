using MedApp.Domain.Attachments;

namespace MedApp.Application.Attachments.Common;

/// <summary>An attachment in a list, with the title of the procedure and the name of the stage it's attached to now.</summary>
public sealed record AttachmentRow(Attachment Attachment, string? ProcedureTitle, string? StageName);
