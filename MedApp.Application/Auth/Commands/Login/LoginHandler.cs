using MedApp.Application.Common.Authentication;
using MediatR;

namespace MedApp.Application.Auth.Commands.Login;

public sealed class LoginHandler(
    IAuthenticationService authenticationService,
    IJwtTokenGenerator tokenGenerator)
    : IRequestHandler<LoginCommand, string>
{
    public async Task<string> Handle(LoginCommand request, CancellationToken ct)
    {
        var (userId, username, roles) =
            await authenticationService.ValidateCredentialsAsync(
                request.Username,
                request.Password);

        return tokenGenerator.GenerateToken(userId, username, roles);
    }
}
