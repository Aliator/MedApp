using System.Net;
using FluentAssertions;
using MedApp.API.Common.Extensions;
using MedApp.API.ServerRendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedApp.UnitTests.Api.ServerRendering;

/// <summary>
/// The HttpClient that forwards page requests to the Node server, as the API registers it (with the service
/// defaults, which give every other client retries), calling a real server on this machine that always fails.
/// </summary>
[TestFixture]
public sealed class SsrClientTests
{
    private WebApplication _renderer = null!;
    private int _requests;

    [SetUp]
    public async Task StartRenderer()
    {
        _requests = 0;
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _renderer = builder.Build();
        _renderer.Run(context =>
        {
            Interlocked.Increment(ref _requests);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });
        await _renderer.StartAsync();
    }

    [TearDown]
    public async Task StopRenderer() => await _renderer.DisposeAsync();

    [Test]
    public async Task CreateClient_RendererFails_AsksItOnceWithoutRetrying()
    {
        // The API's services, as Program.cs adds them (enough for its HttpClients).
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.AddServiceDefaults();
        builder.Services.AddApi(builder.Configuration);
        await using var api = builder.Build();
        var client = api.Services.GetRequiredService<IHttpClientFactory>().CreateClient(SsrPages.ClientName);

        using var response = await client.GetAsync(new Uri(new Uri(_renderer.Urls.First()), "/patients"));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        _requests.Should().Be(1);
    }
}
