using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetUserRoles;

public sealed record GetUserRolesQuery(
    string Username
) : IRequest<IReadOnlyList<string>>;