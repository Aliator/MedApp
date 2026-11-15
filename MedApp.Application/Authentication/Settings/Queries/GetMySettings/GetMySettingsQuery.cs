using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Settings.Queries.GetMySettings;

/// <summary>The signed-in user's app settings.</summary>
public sealed record GetMySettingsQuery(Guid UserId) : IRequest<UserSettingsResponse>;
