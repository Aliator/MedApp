using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetAllUsers;

public sealed record GetAllUsersQuery
    : IRequest<IReadOnlyList<string>>;