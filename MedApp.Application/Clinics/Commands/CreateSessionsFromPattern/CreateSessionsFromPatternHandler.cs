using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Domain.Clinics;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CreateSessionsFromPattern;

/// <summary>
/// Makes each date's session holding the clinician's and the room's locks, checking it as adding one by hand does
/// (<see cref="SessionPlacement"/>): a date that fails is skipped with the reason, and the rest are still made. Nothing
/// is made in the background: this runs only when someone asks.
/// </summary>
public sealed class CreateSessionsFromPatternHandler(
    IClinicRepository repository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSessionsFromPatternCommand, CreateSessionsResponse>
{
    public async Task<CreateSessionsResponse> Handle(CreateSessionsFromPatternCommand request, CancellationToken cancellationToken)
    {
        var pattern = await repository.GetPatternAsync(request.PatternId, cancellationToken)
                      ?? throw new NotFoundException($"Clinic pattern '{request.PatternId}' was not found.");
        var staffUsername = await identityReadService.GetUsernameOrThrowAsync(pattern.StaffUserId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dates = ClinicPatternRules.Dates(pattern, UkTime.DateOf(now), request.Weeks);
        var response = new CreateSessionsResponse();
        if (dates.Count == 0)
            return response;

        // An archived room or team can't be chosen for anything new: every date is skipped, saying why.
        var archived = pattern.Room.IsArchived ? ClinicRules.RoomArchived(pattern.Room)
            : pattern.Team.IsArchived ? $"The '{pattern.Team.Name}' team is archived."
            : null;
        if (archived is not null)
        {
            response.Skipped.AddRange(dates.Select(d => new SkippedSessionResponse { Date = d, Reason = archived }));
            return response;
        }

        var made = await repository.GetPatternDatesAsync(pattern.Id, dates[0], dates[^1], cancellationToken);
        foreach (var date in dates)
        {
            var (createdId, why) = await CreateAsync(pattern, date, made, request.Actor, now, cancellationToken);
            if (createdId is { } id && await repository.GetSessionAsync(id, cancellationToken) is { } session)
                response.Created.Add(ClinicMapper.ToResponse(session, staffUsername, [], now, request.Actor.CanManageClinics));
            else
                response.Skipped.Add(new SkippedSessionResponse { Date = date, Reason = why ?? ClinicPatternRules.AlreadyMade });
        }

        return response;
    }

    /// <summary>Makes the date's session and returns its ID, or says why not.</summary>
    private async Task<(Guid? CreatedId, string? Why)> CreateAsync(
        ClinicPattern pattern,
        DateOnly date,
        IReadOnlySet<DateOnly> made,
        CalendarActor actor,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (made.Contains(date))
            return (null, ClinicPatternRules.AlreadyMade);

        var start = UkTime.ToUtc(date, pattern.Starts);
        var end = UkTime.ToUtc(date, pattern.Ends);
        if (start <= now)
            return (null, ClinicPatternRules.AlreadyStarted);

        var session = new ClinicSession
        {
            Id = Guid.NewGuid(),
            StaffUserId = pattern.StaffUserId,
            RoomId = pattern.RoomId,
            TeamId = pattern.TeamId,
            PatternId = pattern.Id,
            Date = date,
            StartUtc = start,
            EndUtc = end,
            SlotMinutes = pattern.SlotMinutes,
            Status = ClinicSessionStatus.Planned,
            CreatedAtUtc = now,
            CreatedByUserId = actor.UserId,
            LastUpdated = now
        };

        try
        {
            var why = await calendarLock.RunAsync<string?>(SessionPlacement.Locks(pattern.StaffUserId, pattern.RoomId), async ct =>
            {
                if (await SessionPlacement.WhyNotAsync(calendarRepository, repository, pattern.StaffUserId, pattern.RoomId, start, end, ct) is { } reason)
                    return reason;

                await repository.AddSessionAsync(session, ct);
                return null;
            }, cancellationToken);
            return why is null ? (session.Id, null) : (null, why);
        }
        catch (ConflictException e)
        {
            // Another request made the date's session first, or a lock wasn't granted in time.
            return (null, e.Message);
        }
    }
}
