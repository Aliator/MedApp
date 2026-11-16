using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Demographics.Common;
using MedApp.Contracts.Demographics.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MediatR;

namespace MedApp.Application.Demographics.Queries.LookUpDemographics;

/// <summary>
/// Looks someone up in the demographics service. Every lookup is recorded in the activity log, with whether anyone
/// was found but never the number. A 404 when no lookup is set up, or when the service has nobody with the number.
/// </summary>
public sealed class LookUpDemographicsHandler(IPersonalDemographicsService demographics, IActivityLog activityLog)
    : IRequestHandler<LookUpDemographicsQuery, DemographicsLookupResponse>
{
    public const string NotSetUp = "Looking up NHS numbers isn't set up.";

    public const string NotFound =
        "Nobody with this NHS number was found. Check the number, or enter their details yourself.";

    public async Task<DemographicsLookupResponse> Handle(LookUpDemographicsQuery request, CancellationToken cancellationToken)
    {
        if (!demographics.IsAvailable)
            throw new NotFoundException(NotSetUp);

        var nhsNumber = NhsNumbers.Normalise(request.NhsNumber)!;
        var record = await demographics.LookUpAsync(nhsNumber, cancellationToken);

        await activityLog.RecordAsync(
            activityLog.Entry(ActivityAction.DemographicsLookedUp, details: new { found = record is not null }),
            cancellationToken);

        return record is null
            ? throw new NotFoundException(NotFound)
            : DemographicsMapper.ToResponse(record, demographics.Source ?? string.Empty);
    }
}
