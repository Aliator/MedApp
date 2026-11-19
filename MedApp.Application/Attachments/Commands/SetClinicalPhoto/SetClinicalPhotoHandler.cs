using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Attachments.Responses;
using MediatR;

namespace MedApp.Application.Attachments.Commands.SetClinicalPhoto;

/// <summary>
/// Marks a picture as a clinical photo, or unmarks it (MC27): a live PNG, JPEG, HEIC or HEIF on a pathway or one of its
/// stages (else a 409), seen by the caller (else the recorded 404) and its pathway their team's (else a 409), while the
/// record and the pathway allow its files to change (a 409). Marking needs a Valid consent to clinical photography on
/// the pathway (<see cref="ClinicalPhotography"/>, a 409 without); unmarking never does. Setting what it already is
/// changes nothing. The photo is shown as now (a thumbnail or a download), never inline at full size, and never analysed.
/// </summary>
public sealed class SetClinicalPhotoHandler(
    IAttachmentRepository attachments,
    IPatientRepository patients,
    IPatientPathwayRepository pathways,
    IConsentRepository consents,
    ITeamBoundary boundary)
    : IRequestHandler<SetClinicalPhotoCommand, AttachmentResponse>
{
    public async Task<AttachmentResponse> Handle(SetClinicalPhotoCommand request, CancellationToken cancellationToken)
    {
        var notFound = AttachmentWalls.NotFoundMessage(request.Id);
        var attachment = await attachments.GetForUpdateAsync(request.Id, cancellationToken);
        if (attachment is null || attachment.IsDeleted)
            throw new NotFoundException(notFound);
        await AttachmentWalls.EnsureVisibleAsync(attachment, boundary, request.Actor.Teams, cancellationToken);

        if (!ClinicalPhotography.CanBeClinicalPhoto(attachment))
            throw new ConflictException(ClinicalPhotography.NotAPictureMessage);

        var target = await AttachmentTargets.OfAsync(attachment, patients, pathways, attachments, cancellationToken);
        if (target.Pathway is null)
            throw new ConflictException(ClinicalPhotography.NotAPictureMessage);
        await boundary.EnsureCanChangePathwayAsync(target.Pathway, request.Actor.Teams, notFound, cancellationToken);
        PatientRules.EnsureCanChange(target.Owner);
        if (target.WhyFilesCantChange() is { } reason)
            throw new ConflictException(reason);

        if (attachment.IsClinicalPhoto != request.IsClinicalPhoto)
        {
            if (request.IsClinicalPhoto
                && !ClinicalPhotography.HasValidConsent(await consents.GetForPathwayAsync(attachment.PathwayId!.Value, cancellationToken)))
                throw new ConflictException(ClinicalPhotography.NoConsentMessage);

            attachment.IsClinicalPhoto = request.IsClinicalPhoto;
            await attachments.SaveAsync(cancellationToken);
        }

        return AttachmentMapper.ToResponse(
            attachment, target, request.Actor.UserId, request.CanCorrectRecords, canAttachFiles: true);
    }
}
