using MedApp.Application.Authentication.Sessions.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Queries.GetSession;

/// <summary>
/// The session's expiry. Reading it doesn't extend it: only <c>ExtendSessionCommand</c> does. A session that has
/// ended (or never existed) is a <see cref="NotFoundException"/>.
/// </summary>
public sealed class GetSessionHandler(
    ISessionService sessionService,
    TimeProvider timeProvider)
    : IRequestHandler<GetSessionQuery, SessionResponse>
{
    public async Task<SessionResponse> Handle(GetSessionQuery request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = await sessionService.GetActiveSessionAsync(request.SessionId, now, ct)
                      ?? throw new NotFoundException("The session has ended.");

        return SessionMapper.ToResponse(session, now);
    }
}
