using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Attachments.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachments;

public sealed class GetAttachmentsHandler(
    IAttachmentRepository attachments,
    IPatientRepository patients,
    IPatientPathwayRepository pathways,
    IActivityLog activityLog)
    : IRequestHandler<GetAttachmentsQuery, AttachmentListResponse>
{
    public async Task<AttachmentListResponse> Handle(GetAttachmentsQuery request, CancellationToken cancellationToken)
    {
        // A 404 for a patient, pathway, stage or referral that doesn't exist, rather than an empty list.
        var target = await AttachmentTargets.ResolveAsync(
            request.PatientId, request.PathwayId, request.StageId, request.ReferralId, patients, pathways, attachments, cancellationToken);

        var (rows, totalCount) = await attachments.ListAsync(request, cancellationToken);

        // Files' names and details are the patient's data: recorded like other views (deduplicated over its window).
        await activityLog.RecordViewsAsync(
            ActivityAction.AttachmentsViewed, [target.PatientId], cancellationToken, target.Pathway?.Id);
        return AttachmentMapper.ToListResponse(rows, totalCount, request.ViewerId, request.CanCorrectRecords, request.CanAttachFiles);
    }
}
