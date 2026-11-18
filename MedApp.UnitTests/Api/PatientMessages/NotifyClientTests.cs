using FluentAssertions;
using MedApp.API.Common.Extensions;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.Appointments;
using MedApp.Infrastructure.PatientMessages;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace MedApp.UnitTests.Api.PatientMessages;

/// <summary>
/// The HttpClient GOV.UK Notify's messages go through, as the API registers it (with the service defaults, which give
/// every other client retries), against a real server on this machine: a POST is made once, never retried (Notify has no
/// idempotency key), and a timeout is unconfirmed, not an error the job would try again.
/// </summary>
[TestFixture]
public sealed class NotifyClientTests
{
    private const string ApiKey = "medapp_test-26785a09-ab16-4eb0-8407-a37497a57506-3d844edf-8d35-48ac-975b-e847b4f122b0";

    private WebApplication _notify = null!;
    private int _requests;
    private TimeSpan _delay;
    private int _status;

    [SetUp]
    public async Task StartNotify()
    {
        _requests = 0;
        _delay = TimeSpan.Zero;
        _status = StatusCodes.Status500InternalServerError;
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _notify = builder.Build();
        _notify.Run(async context =>
        {
            Interlocked.Increment(ref _requests);
            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay);
            context.Response.StatusCode = _status;
        });
        await _notify.StartAsync();
    }

    [TearDown]
    public async Task StopNotify() => await _notify.DisposeAsync();

    private async Task<(WebApplication Api, NotifyPatientMessageTransport Transport)> TransportAsync(TimeSpan timeout)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.AddServiceDefaults();
        builder.Services.AddApi(builder.Configuration);
        builder.Services.AddHttpClient(NotifyPatientMessageTransport.ClientName, client => client.Timeout = timeout);
        var api = builder.Build();
        await Task.CompletedTask;
        var transport = new NotifyPatientMessageTransport(
            api.Services.GetRequiredService<IHttpClientFactory>(),
            MsOptions.Create(new NotifyOptions
            {
                BaseUrl = _notify.Urls.First(),
                ApiKey = ApiKey,
                SmsTemplateId = Guid.NewGuid().ToString(),
                EmailTemplateId = Guid.NewGuid().ToString(),
            }),
            new FixedTimeProvider(Now));
        return (api, transport);
    }

    private static MedApp.Domain.PatientMessages.PatientMessage Text() =>
        PatientMessagesTestConstants.Message(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)));

    [Test]
    public async Task Send_NotifyFails_PostsOnceWithoutRetrying()
    {
        var (api, transport) = await TransportAsync(TimeSpan.FromSeconds(30));
        await using var _ = api;

        var act = () => transport.SendAsync(Text(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        _requests.Should().Be(1);
    }

    [Test]
    public async Task Send_NoAnswerInTime_IsUnconfirmed_AndPostedOnce()
    {
        _delay = TimeSpan.FromSeconds(3);
        _status = StatusCodes.Status201Created;
        var (api, transport) = await TransportAsync(TimeSpan.FromMilliseconds(500));
        await using var _ = api;

        var result = await transport.SendAsync(Text(), CancellationToken.None);

        result.Outcome.Should().Be(PatientMessageSendOutcome.Unconfirmed);
        result.Reason.Should().Be(PatientMessageReasons.Unconfirmed);
        await Task.Delay(TimeSpan.FromSeconds(1));
        _requests.Should().Be(1);
    }

    [Test]
    public async Task Send_CallersOwnCancellation_IsntUnconfirmed()
    {
        _delay = TimeSpan.FromSeconds(3);
        var (api, transport) = await TransportAsync(TimeSpan.FromSeconds(30));
        await using var _ = api;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var act = () => transport.SendAsync(Text(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
