using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.ResetUserPassword;

public sealed record ResetUserPasswordCommand(
    string Username
) : IRequest<ResetUserPasswordResponse>;