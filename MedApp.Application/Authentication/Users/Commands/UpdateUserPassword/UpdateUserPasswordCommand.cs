using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;

/// <summary>
/// A user changes their own password. Once it's changed, their other sessions end; <paramref name="CurrentSessionId"/>,
/// the one they changed it from, carries on.
/// </summary>
public sealed record UpdateUserPasswordCommand(
    string Username,
    string OldPassword,
    string NewPassword,
    Guid? CurrentSessionId = null
) : IRequest<IdentityResult>;
