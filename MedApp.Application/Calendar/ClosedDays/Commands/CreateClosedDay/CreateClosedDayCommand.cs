using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

/// <summary>Closes the clinic on a UK date, today or later, with a name ("Staff training day"). One per date.</summary>
public sealed record CreateClosedDayCommand(DateOnly Date, string Name, CalendarActor Actor) : IRequest<ClosedDayResponse>;
