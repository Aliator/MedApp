using MediatR;

namespace MedApp.Application.Auth.Queries.GetAllRoles;

public sealed record GetAllRolesQuery
    : IRequest<IReadOnlyList<string>>;