using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Attachments.Responses;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachments;

public sealed class GetAttachmentsHandler(
    IAttachmentRepository attachments,
    IPatientRepository patients,
    IPatientProcedureRepository procedures)
    : IRequestHandler<GetAttachmentsQuery, AttachmentListResponse>
{
    public async Task<AttachmentListResponse> Handle(GetAttachmentsQuery request, CancellationToken cancellationToken)
    {
        // A 404 for a patient, procedure or stage that doesn't exist, rather than an empty list.
        _ = await AttachmentTargets.ResolveAsync(
            request.PatientId, request.ProcedureId, request.StageId, patients, procedures, attachments, cancellationToken);

        var (rows, totalCount) = await attachments.ListAsync(request, cancellationToken);
        return AttachmentMapper.ToListResponse(rows, totalCount, request.ViewerId, request.IsAdmin);
    }
}
