using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.SetSiteArchived;

public sealed class SetSiteArchivedHandler(
    ISiteRepository repository, IWorkingHoursRepository hours, TimeProvider timeProvider)
    : IRequestHandler<SetSiteArchivedCommand, SiteResponse>
{
    public async Task<SiteResponse> Handle(SetSiteArchivedCommand request, CancellationToken cancellationToken)
    {
        var site = await repository.GetSiteOrThrowAsync(request.SiteId, cancellationToken);

        if (site.IsArchived != request.IsArchived)
        {
            if (request.IsArchived)
            {
                SiteRules.EnsureNotLastOpenSite(site, await repository.CountOpenSitesAsync(cancellationToken));

                var openRooms = await repository.GetRoomsAsync(site.Id, includeArchived: false, cancellationToken);
                if (openRooms.Count > 0)
                    throw new ConflictException(SiteRules.HasOpenRooms(site, openRooms));

                // Staff working there today or later would work at a site nobody can choose.
                var today = UkTime.Today(timeProvider);
                if ((await hours.GetStaffHoursAtSiteAsync(site.Id, today, cancellationToken)).Count > 0)
                    throw new ConflictException(SiteRules.HasStaffHours(site));
            }

            site.IsArchived = request.IsArchived;
            site.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateSiteAsync(site, cancellationToken);
        }

        return await SiteMapper.ToResponseAsync(site, repository, cancellationToken);
    }
}
