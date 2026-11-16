using FluentAssertions;
using MailKit.Security;
using MedApp.Application.Common.Email;
using MedApp.Infrastructure.Common.Email;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Infrastructure.Common.Email;

[TestFixture]
public sealed class EmailDeliveryTests
{
    private static EmailDeliveryOptions Configured(int capacity = 10) => new()
    {
        FromAddress = "medapp@example.test",
        FromName = "MedApp",
        Smtp = new SmtpOptions { Host = "smtp.example.test" },
        QueueCapacity = capacity,
    };

    private static EmailMessage Message(string subject = "Subject") =>
        new(Address, subject, "<p>Hello</p>", "Hello");

    [Test]
    public void CanSend_NeedsAHostAndAFromAddress()
    {
        Configured().CanSend.Should().BeTrue();
        new EmailDeliveryOptions { FromAddress = "a@example.test" }.CanSend.Should().BeFalse();
        new EmailDeliveryOptions { Smtp = new SmtpOptions { Host = "h" } }.CanSend.Should().BeFalse();
    }

    [Test]
    public async Task Queue_Configured_QueuesTheMessage()
    {
        var queue = new EmailQueue(Options.Create(Configured()), NullLogger<EmailQueue>.Instance);

        await queue.SendAsync(Message(), CancellationToken.None);

        queue.CanSend.Should().BeTrue();
        queue.Reader.TryRead(out var queued).Should().BeTrue();
        queued!.Subject.Should().Be("Subject");
    }

    [Test]
    public async Task Queue_NotConfigured_QueuesNothing()
    {
        var queue = new EmailQueue(Options.Create(new EmailDeliveryOptions()), NullLogger<EmailQueue>.Instance);

        await queue.SendAsync(Message(), CancellationToken.None);

        queue.CanSend.Should().BeFalse();
        queue.Reader.TryRead(out _).Should().BeFalse();
    }

    [Test]
    public async Task Queue_Full_DropsTheMessageWithoutThrowingOrWaiting()
    {
        var queue = new EmailQueue(Options.Create(Configured(capacity: 1)), NullLogger<EmailQueue>.Instance);

        await queue.SendAsync(Message("first"), CancellationToken.None);
        var second = queue.SendAsync(Message("second"), CancellationToken.None);

        second.IsCompleted.Should().BeTrue();
        queue.Reader.TryRead(out var kept).Should().BeTrue();
        kept!.Subject.Should().Be("first");
        queue.Reader.TryRead(out _).Should().BeFalse();
    }

    [Test]
    public async Task Dispatcher_SendsEachQueuedMessage_AndCarriesOnAfterAFailure()
    {
        var queue = new EmailQueue(Options.Create(Configured()), NullLogger<EmailQueue>.Instance);
        var transport = new Mock<IEmailTransport>();
        var sent = new List<string>();
        var allDone = new TaskCompletionSource();
        transport.Setup(t => t.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns<EmailMessage, CancellationToken>((m, _) =>
            {
                sent.Add(m.Subject);
                if (m.Subject == "fails")
                    throw new InvalidOperationException("The mail server is down.");
                if (m.Subject == "last")
                    allDone.SetResult();
                return Task.CompletedTask;
            });
        using var dispatcher = new EmailDispatcher(queue, transport.Object, NullLogger<EmailDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        await queue.SendAsync(Message("fails"), CancellationToken.None);
        await queue.SendAsync(Message("last"), CancellationToken.None);
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await dispatcher.StopAsync(CancellationToken.None);

        sent.Should().Equal("fails", "last");
    }

    [Test]
    public async Task Dispatcher_Stopping_EndsQuietly()
    {
        var queue = new EmailQueue(Options.Create(Configured()), NullLogger<EmailQueue>.Instance);
        using var dispatcher = new EmailDispatcher(queue, Mock.Of<IEmailTransport>(), NullLogger<EmailDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        var stop = () => dispatcher.StopAsync(CancellationToken.None);

        await stop.Should().NotThrowAsync();
    }

    [Test]
    public void BuildMessage_FromTheConfiguredAddressWithBothBodies()
    {
        var mime = SmtpEmailTransport.BuildMessage(Message(), Configured());

        mime.From.Mailboxes.Single().Address.Should().Be("medapp@example.test");
        mime.From.Mailboxes.Single().Name.Should().Be("MedApp");
        mime.To.Mailboxes.Single().Address.Should().Be(Address);
        mime.Subject.Should().Be("Subject");
        mime.HtmlBody.Should().Be("<p>Hello</p>");
        mime.TextBody.Should().Be("Hello");
        mime.Body.Should().BeOfType<MultipartAlternative>();
    }

    [Test]
    public void BuildMessage_NoFromAddress_Throws()
    {
        var act = () => SmtpEmailTransport.BuildMessage(Message(), new EmailDeliveryOptions());

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task Transport_NoHost_Throws()
    {
        var transport = new SmtpEmailTransport(Options.Create(new EmailDeliveryOptions()));

        var act = () => transport.SendAsync(Message(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestCase("None", SecureSocketOptions.None)]
    [TestCase("starttls", SecureSocketOptions.StartTls)]
    [TestCase(" SslOnConnect ", SecureSocketOptions.SslOnConnect)]
    [TestCase("Auto", SecureSocketOptions.Auto)]
    [TestCase(null, SecureSocketOptions.Auto)]
    [TestCase("nonsense", SecureSocketOptions.Auto)]
    public void SecurityFor_MapsTheSetting(string? setting, SecureSocketOptions expected) =>
        SmtpEmailTransport.SecurityFor(setting).Should().Be(expected);

    [Test]
    public void QrCode_IsASquareSvgPathWithAQuietZone()
    {
        var image = new QrCodeRenderer().Render("otpauth://totp/MedApp:someone?secret=JBSWY3DPEHPK3PXP&issuer=MedApp");

        image.Size.Should().BeGreaterThan(21 + 2 * QrCodeRenderer.Border - 1);
        image.Path.Should().StartWith("M").And.MatchRegex("^[MHVhvZz0-9 ,.-]+$");
    }
}
