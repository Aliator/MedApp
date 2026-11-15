using MedApp.Application.Authentication.Settings.Repositories;
using MediatR;

namespace MedApp.Application.Authentication.Settings.Commands.SaveMySettings;

public sealed class SaveMySettingsHandler(IUserSettingsRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SaveMySettingsCommand>
{
    public async Task Handle(SaveMySettingsCommand request, CancellationToken cancellationToken) =>
        await repository.SaveAsync(request.UserId, request.Settings, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}
