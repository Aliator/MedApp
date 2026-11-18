using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetRoles;

/// <summary>Every role (archived ones too), Admin first then by name, each with its permissions and how many hold it.</summary>
public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleResponse>>;
