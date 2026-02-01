using MediatR;

namespace MedApp.Application.Auth.Queries.GetAllUsers;

public sealed record GetAllUsersQuery
    : IRequest<IReadOnlyList<string>>;