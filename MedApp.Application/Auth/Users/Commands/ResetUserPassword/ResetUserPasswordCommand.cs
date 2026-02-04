using MedApp.Contracts.Auth.Responses;
using MediatR;

namespace MedApp.Application.Auth.Users.Commands.ResetUserPassword;

public sealed record ResetUserPasswordCommand(
    string Username
) : IRequest<ResetUserPasswordResponse>;