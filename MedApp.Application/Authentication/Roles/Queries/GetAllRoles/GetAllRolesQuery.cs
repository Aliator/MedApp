using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetAllRoles;

public sealed record GetAllRolesQuery
    : IRequest<IReadOnlyList<string>>;