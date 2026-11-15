using MediatR;

namespace MedApp.Application.Authentication.Settings.Commands.SaveMySettings;

/// <summary>Saves the signed-in user's app settings (a JSON object), replacing any they had.</summary>
public sealed record SaveMySettingsCommand(Guid UserId, string Settings) : IRequest;
