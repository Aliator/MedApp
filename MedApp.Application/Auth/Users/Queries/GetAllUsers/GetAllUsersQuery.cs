using MediatR;

namespace MedApp.Application.Auth.Users.Queries.GetAllUsers;

public sealed record GetAllUsersQuery
    : IRequest<IReadOnlyList<string>>;