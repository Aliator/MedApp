using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Security.Queries.GetSecuritySettings;

/// <summary>The deployment's sign-in rules, for admins.</summary>
public sealed record GetSecuritySettingsQuery : IRequest<SecuritySettingsResponse>;
