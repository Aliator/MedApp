using MedApp.Application.Common.Authentication;
using MediatR;

namespace MedApp.Application.Auth.Sessions.Commands.Logout;

public sealed class LogoutHandler(
    ISessionService sessionService)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(
        LogoutCommand request,
        CancellationToken ct)
    {
        if (request.SessionId is null)
            return;

        await sessionService.RevokeSessionAsync(
            request.SessionId.Value,
            ct);
    }
}