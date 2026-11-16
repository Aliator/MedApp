using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Attachments.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachments;

public sealed class GetAttachmentsHandler(
    IAttachmentRepository attachments,
    IPatientRepository patients,
    IPatientProcedureRepository procedures,
    IActivityLog activityLog)
    : IRequestHandler<GetAttachmentsQuery, AttachmentListResponse>
{
    public async Task<AttachmentListResponse> Handle(GetAttachmentsQuery request, CancellationToken cancellationToken)
    {
        // A 404 for a patient, procedure or stage that doesn't exist, rather than an empty list.
        var target = await AttachmentTargets.ResolveAsync(
            request.PatientId, request.ProcedureId, request.StageId, patients, procedures, attachments, cancellationToken);

        var (rows, totalCount) = await attachments.ListAsync(request, cancellationToken);

        // Files' names and details are the patient's data: recorded like other views (deduplicated over its window).
        await activityLog.RecordViewsAsync(
            ActivityAction.AttachmentsViewed, [target.PatientId], cancellationToken, target.Procedure?.Id);
        return AttachmentMapper.ToListResponse(rows, totalCount, request.ViewerId, request.IsAdmin);
    }
}
