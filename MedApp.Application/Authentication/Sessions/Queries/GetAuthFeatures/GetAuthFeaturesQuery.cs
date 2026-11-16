using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Queries.GetAuthFeatures;

/// <summary>What sign-in offers here (for pages shown before signing in): whether email with links can be sent.</summary>
public sealed record GetAuthFeaturesQuery : IRequest<AuthFeaturesResponse>;
