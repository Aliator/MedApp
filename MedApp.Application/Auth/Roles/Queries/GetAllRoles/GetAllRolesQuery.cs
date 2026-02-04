using MediatR;

namespace MedApp.Application.Auth.Roles.Queries.GetAllRoles;

public sealed record GetAllRolesQuery
    : IRequest<IReadOnlyList<string>>;