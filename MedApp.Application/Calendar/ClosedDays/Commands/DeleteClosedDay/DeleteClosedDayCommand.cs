using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.DeleteClosedDay;

/// <summary>Opens the clinic again on a closed day, today or later: templates apply there again.</summary>
public sealed record DeleteClosedDayCommand(Guid ClosedDayId) : IRequest;
