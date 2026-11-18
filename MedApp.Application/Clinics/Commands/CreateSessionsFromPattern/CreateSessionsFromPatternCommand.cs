using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CreateSessionsFromPattern;

/// <summary>
/// Create sessions (centre plan decision 125): makes a pattern's sessions for the next <paramref name="Weeks"/> weeks (1 to
/// 13), skipping dates it has already made one on, closed days, days the clinician doesn't work, and clashes, and says
/// what it made and what it skipped and why.
/// </summary>
public sealed record CreateSessionsFromPatternCommand(Guid PatternId, int Weeks, CalendarActor Actor)
    : IRequest<CreateSessionsResponse>;
