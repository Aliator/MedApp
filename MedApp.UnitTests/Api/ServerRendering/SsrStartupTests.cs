using FluentAssertions;
using MedApp.API.Common.Extensions;
using MedApp.API.ServerRendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static MedApp.UnitTests.Common.Constants.SsrTestConstants;

namespace MedApp.UnitTests.Api.ServerRendering;

/// <summary>The "Ssr" settings as the API registers them (<c>AddApi</c>), read when it starts.</summary>
[TestFixture]
public sealed class SsrStartupTests
{
    private static WebApplication Api(string? serverUrl)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        if (serverUrl is not null) builder.Configuration["Ssr:ServerUrl"] = serverUrl;
        // The API's own services, as Program.cs adds them (enough to start without a database).
        builder.Services.AddApi(builder.Configuration);
        return builder.Build();
    }

    [Test]
    public async Task Start_ServerUrlNotAServersAddress_FailsSayingWhy()
    {
        await using var api = Api(PageServerUrl + "/pages");

        var start = () => api.StartAsync();

        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().ContainSingle().Which.Should().Be(SsrOptions.InvalidServerUrl);
    }

    [Test]
    public async Task Start_ServerUrlSet_ForwardsPagesThereWithoutStartingNode()
    {
        await using var api = Api(PageServerUrl);

        await api.StartAsync();
        var ssr = api.Services.GetRequiredService<SsrServer>();
        await ssr.ExecuteTask!;

        ssr.Address.Should().Be(new Uri(PageServerAddress));
        api.Services.GetRequiredService<ISsrServer>().Should().BeSameAs(ssr);
        await api.StopAsync();
    }
}
