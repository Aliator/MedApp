using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserByUsername;

public sealed record GetUserByUsernameQuery(
    string Username
) : IRequest<UserResponse?>;