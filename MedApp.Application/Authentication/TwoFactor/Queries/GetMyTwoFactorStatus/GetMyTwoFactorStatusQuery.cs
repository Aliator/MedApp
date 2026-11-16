using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Queries.GetMyTwoFactorStatus;

/// <summary>Whether the signed-in account uses two-factor authentication, its recovery codes left, and whether it must.</summary>
public sealed record GetMyTwoFactorStatusQuery(
    Guid UserId
) : IRequest<TwoFactorStatusResponse>;
