using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Attachments.Responses;
using MediatR;

namespace MedApp.Application.Attachments.Commands.SetClinicalPhoto;

/// <summary>
/// Marks a picture on a pathway as a clinical photo, or unmarks it (MC27). <paramref name="CanCorrectRecords"/> is
/// whether the caller holds <c>CorrectRecords</c>, for the response's <c>CanDelete</c>.
/// </summary>
public sealed record SetClinicalPhotoCommand(Guid Id, bool IsClinicalPhoto, PathwayActor Actor, bool CanCorrectRecords)
    : IRequest<AttachmentResponse>;
