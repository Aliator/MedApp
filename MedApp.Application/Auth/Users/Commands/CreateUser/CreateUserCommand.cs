using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string Password
) : IRequest<IdentityResult>;