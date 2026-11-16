using System.Text.Json;
using MedApp.API.Common.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// The JSON settings the API writes with, from its own registration (<c>AddApi</c>), so a setting added there later
/// is what the tests serialise with.
/// </summary>
public static class ApiJson
{
    private static readonly Lazy<IServiceProvider> Services = new(() =>
        new ServiceCollection().AddLogging().AddApi(new ConfigurationBuilder().Build()).BuildServiceProvider());

    /// <summary>What the controllers' responses are written with.</summary>
    public static JsonSerializerOptions Controllers =>
        Services.Value.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

    /// <summary>What the notifications hub's messages are written with.</summary>
    public static JsonSerializerOptions Hub =>
        Services.Value.GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions;
}
