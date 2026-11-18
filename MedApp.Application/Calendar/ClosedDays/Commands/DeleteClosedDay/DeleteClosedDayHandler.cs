using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.DeleteClosedDay;

public sealed class DeleteClosedDayHandler(IClosedDayRepository repository, TimeProvider timeProvider)
    : IRequestHandler<DeleteClosedDayCommand>
{
    public async Task Handle(DeleteClosedDayCommand request, CancellationToken cancellationToken)
    {
        var day = await repository.GetByIdAsync(request.ClosedDayId, cancellationToken)
                  ?? throw new NotFoundException($"Closed day '{request.ClosedDayId}' was not found.");

        ClosedDayRules.EnsureNotPast(day.Date, UkTime.Today(timeProvider), ClosedDayRules.PastCantBeRemoved);

        await repository.DeleteAsync(day, cancellationToken);
    }
}
