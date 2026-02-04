using MediatR;

namespace MedApp.Application.Auth.Roles.Queries.GetUserRoles;

public sealed record GetUserRolesQuery(
    string Username
) : IRequest<IReadOnlyList<string>>;