using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.Logout;

public sealed record LogoutCommand(
    Guid? SessionId
) : IRequest;