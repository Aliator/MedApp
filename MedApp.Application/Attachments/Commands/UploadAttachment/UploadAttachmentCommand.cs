using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Attachments.Responses;
using MediatR;

namespace MedApp.Application.Attachments.Commands.UploadAttachment;

/// <summary>
/// Attaches a file to exactly one of a patient, a procedure or a stage. <paramref name="Content"/> is the request's
/// body, read once, as it arrives; <paramref name="ContentLength"/> is what the request said its length is, if it did.
/// </summary>
public sealed record UploadAttachmentCommand(
    Guid? PatientId,
    Guid? ProcedureId,
    Guid? StageId,
    string? FileName,
    long? ContentLength,
    Stream Content,
    ProcedureActor Actor,
    bool IsAdmin)
    : IRequest<AttachmentResponse>;
