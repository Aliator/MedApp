using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CreateClinicPattern;

/// <summary>A new clinic pattern (centre plan decision 125). It makes no sessions until someone asks.</summary>
public sealed record CreateClinicPatternCommand(
    Guid StaffUserId,
    Guid RoomId,
    Guid SpecialtyId,
    string DayOfWeek,
    TimeOnly Starts,
    TimeOnly Ends,
    int SlotMinutes,
    int EveryWeeks,
    DateOnly FromDate,
    DateOnly? UntilDate,
    CalendarActor Actor)
    : IRequest<ClinicPatternResponse>;
