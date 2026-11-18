using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MedApp.Domain.Sites;
using MediatR;

namespace MedApp.Application.Calendar.Hours.Commands.SetStaffHours;

public sealed class SetStaffHoursHandler(
    IWorkingHoursRepository repository,
    ISiteRepository siteRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<SetStaffHoursCommand, StaffHoursResponse>
{
    public async Task<StaffHoursResponse> Handle(SetStaffHoursCommand request, CancellationToken cancellationToken)
    {
        if (!request.Actor.CanSetHours)
            throw new ForbiddenException(CalendarActor.CantSetHours);
        await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);

        var today = UkTime.Today(timeProvider);
        var wanted = (request.Days ?? [])
            .Select(d => new StaffHoursValue(HoursValidation.Day(d.DayOfWeek), d.Starts, d.Ends, d.SiteId))
            .ToList();

        var sites = new Dictionary<Guid, Site>();
        foreach (var siteId in wanted.Select(d => d.SiteId).Distinct())
            sites[siteId] = await siteRepository.GetSiteOrThrowAsync(siteId, cancellationToken);

        // Under the person's calendar lock (bookings check their hours under it) and each site's (its hours can't
        // change meanwhile), so two changes at once can't both add rows from today.
        await calendarLock.RunAsync([request.StaffUserId, .. sites.Keys], async ct =>
        {
            var rows = await repository.GetStaffHoursForUpdateAsync(request.StaffUserId, today, ct);

            // An archived site stays where someone works until their hours change, but can't be chosen again.
            foreach (var site in sites.Values.Where(s => s.IsArchived))
                if (!rows.Any(r => r.SiteId == site.Id && r.IsValidOn(today)))
                    throw new ConflictException(SiteRules.SiteArchived(site));

            var siteHours = await repository.GetSiteHoursOnAsync(sites.Keys.ToList(), today, ct);
            foreach (var day in wanted)
            {
                var site = sites[day.SiteId];
                if (WorkingTimeSet.Opening(siteHours.Where(h => h.SiteId == site.Id).ToList(), day.Day) is not { } open)
                    throw new ConflictException(StaffHoursRules.SiteClosed(site.Name, day.Day));
                if (day.Starts < open.Opens || day.Ends > open.Closes)
                    throw new ConflictException(StaffHoursRules.OutsideSite(site.Name, day.Day, open.Opens, open.Closes));
            }

            var change = TemplateHistory.Replace(
                rows,
                wanted,
                today,
                r => new StaffHoursValue(r.DayOfWeek, r.Starts, r.Ends, r.SiteId),
                (value, validFrom) => new StaffHours
                {
                    Id = Guid.NewGuid(),
                    StaffUserId = request.StaffUserId,
                    DayOfWeek = value.Day,
                    Starts = value.Starts,
                    Ends = value.Ends,
                    SiteId = value.SiteId,
                    ValidFrom = validFrom
                });

            if (change.HasChanges)
                await repository.SaveStaffHoursAsync(change, ct);

            return change.HasChanges;
        }, cancellationToken);

        return await StaffHoursRules.ToResponseAsync(
            request.StaffUserId, today, request.Actor.CanSetHours, repository, siteRepository, cancellationToken);
    }

    private sealed record StaffHoursValue(DayOfWeek Day, TimeOnly Starts, TimeOnly Ends, Guid SiteId);
}
