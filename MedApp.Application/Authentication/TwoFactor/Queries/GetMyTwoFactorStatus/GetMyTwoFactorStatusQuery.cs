using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Queries.GetMyTwoFactorStatus;

/// <summary>
/// Whether the signed-in account uses two-factor authentication and how, its recovery codes left, whether it must, and
/// whether it could use codes by email.
/// </summary>
public sealed record GetMyTwoFactorStatusQuery(
    Guid UserId
) : IRequest<TwoFactorStatusResponse>;
