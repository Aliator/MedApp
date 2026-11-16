using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.DeleteCalendarBlock;

public sealed class DeleteCalendarBlockHandler(ICalendarRepository repository, TimeProvider timeProvider)
    : IRequestHandler<DeleteCalendarBlockCommand>
{
    public async Task Handle(DeleteCalendarBlockCommand request, CancellationToken cancellationToken)
    {
        var block = await repository.GetBlockForUpdateAsync(request.BlockId, cancellationToken)
                    ?? throw new NotFoundException($"Block '{request.BlockId}' was not found.");

        request.Actor.EnsureCanChange(block.StaffUserId);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(block.StartUtc), UkTime.Today(timeProvider));

        await repository.DeleteBlockAsync(block, cancellationToken);
    }
}
