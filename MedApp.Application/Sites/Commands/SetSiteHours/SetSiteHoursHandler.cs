using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MedApp.Domain.Sites;
using MediatR;

namespace MedApp.Application.Sites.Commands.SetSiteHours;

public sealed class SetSiteHoursHandler(
    ISiteRepository siteRepository,
    IWorkingHoursRepository repository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<SetSiteHoursCommand, SiteHoursResponse>
{
    public async Task<SiteHoursResponse> Handle(SetSiteHoursCommand request, CancellationToken cancellationToken)
    {
        var site = await siteRepository.GetOpenSiteOrThrowAsync(request.SiteId, cancellationToken);
        var today = UkTime.Today(timeProvider);
        var wanted = (request.Days ?? [])
            .Select(d => new SiteHoursValue(HoursValidation.Day(d.DayOfWeek), d.Opens, d.Closes))
            .ToList();
        var asRows = wanted
            .Select(v => new SiteHours { SiteId = site.Id, DayOfWeek = v.Day, Opens = v.Opens, Closes = v.Closes })
            .ToList();

        // Under the site's lock, which changing someone's hours there takes too.
        await calendarLock.RunAsync([site.Id], async ct =>
        {
            // Everyone working there today or later must still fit (change their hours first).
            foreach (var hours in await repository.GetStaffHoursAtSiteAsync(site.Id, today, ct))
            {
                var open = WorkingTimeSet.Opening(asRows, hours.DayOfWeek);
                if (open is null || hours.Starts < open.Value.Opens || hours.Ends > open.Value.Closes)
                {
                    var usernames = await identityReadService.GetUsernamesByIdAsync([hours.StaffUserId], ct);
                    var username = usernames.GetValueOrDefault(hours.StaffUserId) ?? "Someone";
                    throw new ConflictException(SiteRules.StaffOutsideHours(site, username, hours.DayOfWeek));
                }
            }

            var rows = await repository.GetSiteHoursForUpdateAsync(site.Id, today, ct);
            var change = TemplateHistory.Replace(
                rows,
                wanted,
                today,
                r => new SiteHoursValue(r.DayOfWeek, r.Opens, r.Closes),
                (value, validFrom) => new SiteHours
                {
                    Id = Guid.NewGuid(),
                    SiteId = site.Id,
                    DayOfWeek = value.Day,
                    Opens = value.Opens,
                    Closes = value.Closes,
                    ValidFrom = validFrom
                });

            if (change.HasChanges)
                await repository.SaveSiteHoursAsync(change, ct);

            return change.HasChanges;
        }, cancellationToken);

        return WorkingHoursMapper.ToResponse(site.Id, await repository.GetSiteHoursOnAsync(site.Id, today, cancellationToken));
    }

    private sealed record SiteHoursValue(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);
}
