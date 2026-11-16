using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using MedApp.Application.Common.Email;
using MedApp.Infrastructure.Common.Email;
using Microsoft.Extensions.Options;

namespace MedApp.IntegrationTests.Infrastructure;

/// <summary>
/// The real SMTP transport (MailKit) against Mailpit in Docker, the mail catcher the AppHost and kind run: a message
/// arrives with its subject, both bodies and the configured sender. The rest of the integration tests keep emails in a
/// fake sender instead.
/// </summary>
[TestFixture]
public sealed class SmtpEmailTransportTests
{
    public const string MailpitImage = "axllent/mailpit:v1.33.0";

    private IContainer _mailpit = null!;

    [OneTimeSetUp]
    public async Task StartMailpitAsync()
    {
        _mailpit = new ContainerBuilder(MailpitImage)
            .WithPortBinding(1025, true)
            .WithPortBinding(8025, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8025).ForPath("/livez")))
            .Build();
        await _mailpit.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopMailpitAsync() => await _mailpit.DisposeAsync();

    [Test]
    public async Task SendAsync_DeliversTheMessageToTheServer()
    {
        var options = new EmailDeliveryOptions
        {
            FromAddress = "medapp@example.test",
            FromName = "MedApp",
            Smtp = new SmtpOptions { Host = _mailpit.Hostname, Port = _mailpit.GetMappedPublicPort(1025), Security = "None" },
        };
        var message = EmailTemplates.ResetLink("someone@example.test", "synthetic.user", "https://medapp.example.test/reset-password#user=x&token=y");

        await new SmtpEmailTransport(Options.Create(options)).SendAsync(message, CancellationToken.None);

        using var http = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        var list = await http.GetFromJsonAsync<JsonElement>("/api/v1/messages");
        var received = list.GetProperty("messages").EnumerateArray().Should().ContainSingle().Subject;
        received.GetProperty("Subject").GetString().Should().Be(EmailTemplates.ResetLinkSubject);
        received.GetProperty("From").GetProperty("Address").GetString().Should().Be("medapp@example.test");
        received.GetProperty("To")[0].GetProperty("Address").GetString().Should().Be("someone@example.test");

        var full = await http.GetFromJsonAsync<JsonElement>($"/api/v1/message/{received.GetProperty("ID").GetString()}");
        full.GetProperty("Text").GetString().Should().Contain("Choose a new password");
        full.GetProperty("HTML").GetString().Should().Contain("<a href=");
    }
}
