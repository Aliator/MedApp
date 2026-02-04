using MedApp.Application.Common.Authentication;
using MediatR;

namespace MedApp.Application.Auth.Commands.Login;

public sealed class LoginHandler(
    IAuthenticationService authenticationService,
    ISessionService sessionService)
    : IRequestHandler<LoginCommand, SessionToken>
{
    public async Task<SessionToken> Handle(LoginCommand request, CancellationToken ct)
    {
        var (userId, _, _) =
            await authenticationService.ValidateCredentialsAsync(
                request.Username,
                request.Password);

        return await sessionService.CreateSessionAsync(
            userId,
            request.IpAddress,
            request.UserAgent,
            ct);
    }
}
