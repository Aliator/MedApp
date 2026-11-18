using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Consent;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Consent.Queries.GetPathwayConsentRecords;

/// <summary>A pathway's consent records, in any status, oldest recorded first, with what the viewer may do.</summary>
public sealed record GetPathwayConsentRecordsQuery(Guid PathwayId, PathwayActor Viewer)
    : IRequest<IReadOnlyList<ConsentRecordResponse>>;

/// <summary>
/// 404 for an unknown pathway. Read on the pathway page, so it records <c>PathwayViewed</c> as the page does
/// (decision 76; deduplicated with it over the view window).
/// </summary>
public sealed class GetPathwayConsentRecordsHandler(
    IPatientPathwayRepository pathways,
    IConsentRepository repository,
    IIdentityReadService identity,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetPathwayConsentRecordsQuery, IReadOnlyList<ConsentRecordResponse>>
{
    public async Task<IReadOnlyList<ConsentRecordResponse>> Handle(
        GetPathwayConsentRecordsQuery request, CancellationToken cancellationToken)
    {
        var pathway = await pathways.GetByIdAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException($"Pathway '{request.PathwayId}' was not found.");

        var records = await repository.GetForPathwayAsync(pathway.Id, cancellationToken);
        await activityLog.RecordViewsAsync(ActivityAction.PathwayViewed, [pathway.PatientId], cancellationToken, pathway.Id);

        var usernames = await identity.GetUsernamesByIdAsync(records.SelectMany(ConsentMapper.PeopleIn).Distinct(), cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return records
            .Select(r =>
            {
                r.Pathway = pathway;
                r.Patient = pathway.Patient;
                return ConsentMapper.ToResponse(r, usernames, request.Viewer, now);
            })
            .ToList();
    }
}
