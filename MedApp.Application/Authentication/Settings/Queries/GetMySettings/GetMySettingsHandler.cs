using MedApp.Application.Authentication.Settings.Repositories;
using MedApp.Contracts.Authentication.Responses;
using MediatR;
using MedApp.Application.Authentication.Settings.Common;

namespace MedApp.Application.Authentication.Settings.Queries.GetMySettings;

public sealed class GetMySettingsHandler(IUserSettingsRepository repository)
    : IRequestHandler<GetMySettingsQuery, UserSettingsResponse>
{
    public async Task<UserSettingsResponse> Handle(GetMySettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(request.UserId, cancellationToken);
        return UserSettingsMapper.ToResponse(settings);
    }
}
