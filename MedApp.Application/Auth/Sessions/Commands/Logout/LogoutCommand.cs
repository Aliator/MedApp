using MediatR;

namespace MedApp.Application.Auth.Sessions.Commands.Logout;

public sealed record LogoutCommand(
    Guid? SessionId
) : IRequest;