using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;

public sealed class CreateWeekTemplateHandler(
    ICalendarSetupRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<CreateWeekTemplateCommand, WeekTemplateResponse>
{
    public async Task<WeekTemplateResponse> Handle(CreateWeekTemplateCommand request, CancellationToken cancellationToken)
    {
        request.Actor.EnsureCanChangeTemplatesOf(request.OwnerUserId);
        await identityReadService.GetUsernameOrThrowAsync(request.OwnerUserId, cancellationToken);

        var name = request.Name.Trim();
        if (await repository.WeekTemplateNameExistsAsync(request.OwnerUserId, name, null, cancellationToken))
            throw new ConflictException($"A week template called '{name}' already exists.");

        var days = request.Days();
        await TemplateChecks.CheckDayTemplatesAsync(repository, request.OwnerUserId, days, new HashSet<Guid>(), cancellationToken);

        var today = UkTime.Today(timeProvider);
        var id = Guid.NewGuid();
        var template = new WeekTemplate
        {
            Id = id,
            OwnerUserId = request.OwnerUserId,
            Name = name,
            Days = days.Select(d => d.ToDay(id, today)).ToList()
        };

        await repository.AddWeekTemplateAsync(template, cancellationToken);

        var saved = await repository.GetWeekTemplateOrThrowAsync(id, today, cancellationToken);
        return CalendarSetupMapper.ToResponse(saved, WeekTemplateUsage.None, today, canEdit: true);
    }
}
