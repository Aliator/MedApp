using MedApp.Contracts.Roles.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Roles.Queries.GetRoleById;

/// <summary>One role, with its permissions and who holds it (404 if there's none with this ID).</summary>
public sealed record GetRoleByIdQuery(Guid RoleId) : IRequest<RoleResponse>;
