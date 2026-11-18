using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Hours.Queries.GetStaffHours;

public sealed class GetStaffHoursHandler(
    IWorkingHoursRepository repository,
    ISiteRepository siteRepository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetStaffHoursQuery, StaffHoursResponse>
{
    public async Task<StaffHoursResponse> Handle(GetStaffHoursQuery request, CancellationToken cancellationToken)
    {
        await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);
        return await StaffHoursRules.ToResponseAsync(
            request.StaffUserId,
            UkTime.Today(timeProvider),
            request.Actor.CanSetHours,
            repository,
            siteRepository,
            cancellationToken);
    }
}
