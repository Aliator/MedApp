using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Commands.UpdateClinicPattern;

/// <summary>
/// Changes a clinic pattern, from the version the caller read (<paramref name="ExpectedLastUpdated"/>, a 409 if it changed
/// since). Sessions it has already made stay as they are; ending it is setting its until date.
/// </summary>
public sealed record UpdateClinicPatternCommand(
    Guid PatternId,
    Guid StaffUserId,
    Guid RoomId,
    Guid TeamId,
    string DayOfWeek,
    TimeOnly Starts,
    TimeOnly Ends,
    int SlotMinutes,
    int EveryWeeks,
    DateOnly FromDate,
    DateOnly? UntilDate,
    DateTime? ExpectedLastUpdated,
    CalendarActor Actor)
    : IRequest<ClinicPatternResponse>;
