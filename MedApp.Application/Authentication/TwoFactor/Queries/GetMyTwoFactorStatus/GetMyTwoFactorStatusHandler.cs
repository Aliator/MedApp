using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Queries.GetMyTwoFactorStatus;

public sealed class GetMyTwoFactorStatusHandler(
    ITwoFactorService twoFactor,
    ISecuritySettingsRepository settings,
    IAccountEmailService emails,
    IEmailSender sender)
    : IRequestHandler<GetMyTwoFactorStatusQuery, TwoFactorStatusResponse>
{
    public async Task<TwoFactorStatusResponse> Handle(GetMyTwoFactorStatusQuery request, CancellationToken ct)
    {
        var state = await twoFactor.GetStateAsync(request.UserId, ct)
                    ?? throw new NotFoundException("Account not found.");

        return TwoFactorMapper.ToResponse(
            state,
            await settings.IsRequiredAsync(state, ct),
            await EmailCodesAvailability.WhyNotAsync(emails, sender, request.UserId, ct));
    }
}
