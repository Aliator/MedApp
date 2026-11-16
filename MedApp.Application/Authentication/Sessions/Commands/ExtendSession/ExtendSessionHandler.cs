using MedApp.Application.Authentication.Sessions.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.ExtendSession;

/// <summary>
/// Extends the session to a full session length from now, capped at the longest a session can last from sign-in
/// (<see cref="Domain.Authentication.UserSession.Extend"/>), and returns its new expiry. A session that has already
/// ended can't be brought back: that's a <see cref="NotFoundException"/>, and its user signs in again.
/// </summary>
public sealed class ExtendSessionHandler(
    ISessionService sessionService,
    TimeProvider timeProvider)
    : IRequestHandler<ExtendSessionCommand, SessionResponse>
{
    public async Task<SessionResponse> Handle(ExtendSessionCommand request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = await sessionService.ExtendSessionAsync(request.SessionId, now, ct)
                      ?? throw new NotFoundException("The session has ended.");

        return SessionMapper.ToResponse(session, now);
    }
}
