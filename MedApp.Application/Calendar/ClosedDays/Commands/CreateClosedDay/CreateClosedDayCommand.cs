using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

/// <summary>
/// Closes the clinic on a UK date, today or later, with a name ("Staff training day"): everywhere, or at one site
/// (<paramref name="SiteId"/>, one that isn't archived). One everywhere per date, and one per site per date; a site
/// can't be closed on a date already closed everywhere.
/// </summary>
public sealed record CreateClosedDayCommand(DateOnly Date, string Name, CalendarActor Actor, Guid? SiteId = null)
    : IRequest<ClosedDayResponse>;
