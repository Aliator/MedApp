using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Commands.DeleteUser;

public sealed record DeleteUserCommand(
    string Username
) : IRequest<IdentityResult>;