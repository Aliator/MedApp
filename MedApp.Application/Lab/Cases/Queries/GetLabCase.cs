using MedApp.Application.Activity.Common;
using MedApp.Application.Lab.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Lab.Cases.Queries;

/// <summary>One lab case, with what <paramref name="Viewer"/> may do with it.</summary>
public sealed record GetLabCaseQuery(Guid LabCaseId, PathwayActor Viewer) : IRequest<LabCaseResponse>;

/// <summary>
/// One lab case: seen by the lab and admins, and a clinic case by its own clinic (through its pathway); anyone else gets
/// the recorded 404. Opening it is recorded as <see cref="ActivityAction.LabCaseViewed"/> (<c>{labCaseId, access}</c>).
/// </summary>
public sealed class GetLabCaseHandler(LabCaseAccess cases, IActivityLog activityLog, TimeProvider timeProvider)
    : IRequestHandler<GetLabCaseQuery, LabCaseResponse>
{
    public async Task<LabCaseResponse> Handle(GetLabCaseQuery request, CancellationToken cancellationToken)
    {
        var labTeamId = await cases.LabTeamIdAsync(cancellationToken);
        var (labCase, access) = await cases.FindAsync(request.LabCaseId, request.Viewer, labTeamId, forUpdate: false, cancellationToken);

        await activityLog.RecordViewAsync(
            ActivityAction.LabCaseViewed, labCase.PatientId, new { labCaseId = labCase.Id, access = access.ToString() }, cancellationToken);

        return await cases.ResponseAsync(labCase, request.Viewer, labTeamId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
