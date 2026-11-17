using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;

public sealed class CreateDayTemplateHandler(
    ICalendarSetupRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<CreateDayTemplateCommand, DayTemplateResponse>
{
    public async Task<DayTemplateResponse> Handle(CreateDayTemplateCommand request, CancellationToken cancellationToken)
    {
        request.Actor.EnsureCanChangeTemplatesOf(request.OwnerUserId);
        await identityReadService.GetUsernameOrThrowAsync(request.OwnerUserId, cancellationToken);

        var name = request.Name.Trim();
        if (await repository.DayTemplateNameExistsAsync(request.OwnerUserId, name, null, cancellationToken))
            throw new ConflictException($"A day template called '{name}' already exists.");

        await TemplateChecks.CheckActivityTypesAsync(repository, request.Blocks, new HashSet<Guid>(), cancellationToken);

        var today = UkTime.Today(timeProvider);
        var id = Guid.NewGuid();
        var template = new DayTemplate
        {
            Id = id,
            OwnerUserId = request.OwnerUserId,
            Name = name,
            Blocks = request.Blocks.Select(b => b.ToBlock(id, today)).ToList()
        };

        await repository.AddDayTemplateAsync(template, cancellationToken);

        var saved = await repository.GetDayTemplateOrThrowAsync(id, today, cancellationToken);
        return CalendarSetupMapper.ToResponse(saved, DayTemplateUsage.None, today, canEdit: true);
    }
}
