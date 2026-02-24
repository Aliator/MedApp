using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserByUsername;

public sealed record GetUserByUsernameQuery(
    string Username
) : IRequest<UserDetails?>;