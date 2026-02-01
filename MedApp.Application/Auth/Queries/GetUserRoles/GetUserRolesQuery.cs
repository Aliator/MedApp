using MediatR;

namespace MedApp.Application.Auth.Queries.GetUserRoles;

public sealed record GetUserRolesQuery(
    string Username
) : IRequest<IReadOnlyList<string>>;