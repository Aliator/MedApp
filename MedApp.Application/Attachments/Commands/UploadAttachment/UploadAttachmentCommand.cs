using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Attachments.Responses;
using MediatR;

namespace MedApp.Application.Attachments.Commands.UploadAttachment;

/// <summary>
/// Attaches a file to exactly one of a patient, a pathway, a stage or a referral. <paramref name="Content"/> is the request's
/// body, read once, as it arrives; <paramref name="ContentLength"/> is what the request said its length is, if it did.
/// </summary>
public sealed record UploadAttachmentCommand(
    Guid? PatientId,
    Guid? PathwayId,
    Guid? StageId,
    Guid? ReferralId,
    string? FileName,
    long? ContentLength,
    Stream Content,
    PathwayActor Actor,
    bool CanCorrectRecords)
    : IRequest<AttachmentResponse>;
