using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Commands.UpdateUserPassword;

public sealed record UpdateUserPasswordCommand(
    string Username,
    string? OldPassword,
    string? NewPassword
) : IRequest<IdentityResult>;