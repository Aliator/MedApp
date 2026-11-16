using MedApp.Application.Common.Email;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Queries.GetAuthFeatures;

public sealed class GetAuthFeaturesHandler(EmailAvailability email)
    : IRequestHandler<GetAuthFeaturesQuery, AuthFeaturesResponse>
{
    public Task<AuthFeaturesResponse> Handle(GetAuthFeaturesQuery request, CancellationToken ct) =>
        Task.FromResult(new AuthFeaturesResponse(email.CanSendLinks));
}
