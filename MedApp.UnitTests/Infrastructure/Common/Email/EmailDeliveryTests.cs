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
        queue.TryTakeNext()!.Subject.Should().Be("Subject");
    }

    [Test]
    public async Task Queue_NotConfigured_QueuesNothing()
    {
        var queue = new EmailQueue(Options.Create(new EmailDeliveryOptions()), NullLogger<EmailQueue>.Instance);

        await queue.SendAsync(Message(), CancellationToken.None);

        queue.CanSend.Should().BeFalse();
        queue.TryTakeNext().Should().BeNull();
    }

    [Test]
    public async Task Queue_Full_DropsTheMessageWithoutThrowingOrWaiting()
    {
        var queue = new EmailQueue(Options.Create(Configured(capacity: 1)), NullLogger<EmailQueue>.Instance);

        await queue.SendAsync(Message("first"), CancellationToken.None);
        var second = queue.SendAsync(Message("second"), CancellationToken.None);

        second.IsCompleted.Should().BeTrue();
        queue.TryTakeNext()!.Subject.Should().Be("first");
        queue.TryTakeNext().Should().BeNull();
    }

    [Test]
    public async Task Queue_NoticesComeBeforeLinks_AndHaveTheirOwnRoom()
    {
        var queue = new EmailQueue(Options.Create(Configured(capacity: 1)), NullLogger<EmailQueue>.Instance);

        await queue.SendAsync(Message("link"), CancellationToken.None);
        await queue.SendAsync(Message("another link"), CancellationToken.None);
        await queue.SendAsync(Message("notice") with { Category = EmailCategory.Notice }, CancellationToken.None);

        queue.TryTakeNext()!.Subject.Should().Be("notice");
        queue.TryTakeNext()!.Subject.Should().Be("link");
        queue.TryTakeNext().Should().BeNull();
    }

    [Test]
    public async Task Queue_CodesComeBeforeLinks()
    {
        var queue = new EmailQueue(Options.Create(Configured()), NullLogger<EmailQueue>.Instance);

        await queue.SendAsync(Message("link"), CancellationToken.None);
        await queue.SendAsync(Message("code") with { Category = EmailCategory.Code }, CancellationToken.None);

        queue.TryTakeNext()!.Category.Should().Be(EmailCategory.Code);
        queue.TryTakeNext()!.Subject.Should().Be("link");
    }

    [Test]
    public void Budget_CodesShareTheLinksAllowance_SoNoticesKeepTheirs()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var budget = new EmailBudget(10, clock.Object);

        Enumerable.Range(0, 8).Should().OnlyContain(_ => budget.TryTake(EmailCategory.Code));
        budget.TryTake(EmailCategory.Code).Should().BeFalse();
        budget.TryTake(EmailCategory.Link).Should().BeFalse();
        budget.TryTake(EmailCategory.Notice).Should().BeTrue();
    }

    [Test]
    public async Task Queue_Wait_ReturnsOnceEitherHasOne()
    {
        var queue = new EmailQueue(Options.Create(Configured()), NullLogger<EmailQueue>.Instance);
        var waiting = queue.WaitAsync(CancellationToken.None);
        waiting.IsCompleted.Should().BeFalse();

        await queue.SendAsync(Message("notice") with { Category = EmailCategory.Notice }, CancellationToken.None);

        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await queue.WaitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Budget_KeepsAShareForNotices_AndStartsAgainEachHour()
    {
        var now = new DateTimeOffset(2026, 11, 16, 10, 59, 0, TimeSpan.Zero);
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(() => now);
        var budget = new EmailBudget(10, clock.Object);

        budget.NoticeReserve.Should().Be(2);
        Enumerable.Range(0, 8).Should().OnlyContain(_ => budget.TryTake(EmailCategory.Link));
        budget.TryTake(EmailCategory.Link).Should().BeFalse();
        budget.TryTake(EmailCategory.Notice).Should().BeTrue();
        budget.TryTake(EmailCategory.Notice).Should().BeTrue();
        budget.TryTake(EmailCategory.Notice).Should().BeFalse();

        now = now.AddMinutes(1);
        budget.TryTake(EmailCategory.Link).Should().BeTrue();
        new EmailBudget(0, clock.Object).MaxPerHour.Should().Be(1);
    }

    [Test]
    public void SmtpStatus_NeverTheServersMessage()
    {
        EmailDispatcher.SmtpStatus(new MailKit.Net.Smtp.SmtpCommandException(
                MailKit.Net.Smtp.SmtpErrorCode.RecipientNotAccepted, MailKit.Net.Smtp.SmtpStatusCode.MailboxUnavailable, "550 " + Address + " unknown"))
            .Should().Be("550");
        EmailDispatcher.SmtpStatus(new MailKit.Net.Smtp.SmtpProtocolException("x")).Should().Be("protocol");
        EmailDispatcher.SmtpStatus(new MailKit.ServiceNotAuthenticatedException("x")).Should().Be("not-authenticated");
        EmailDispatcher.SmtpStatus(new InvalidOperationException(Address)).Should().Be("none");
    }

    [Test]
    public async Task Dispatcher_OverTheHoursLimit_DropsTheRest()
    {
        var options = Configured();
        options.MaxPerHour = 1;
        var queue = new EmailQueue(Options.Create(options), NullLogger<EmailQueue>.Instance);
        var transport = new Mock<IEmailTransport>();
        var sent = new List<string>();
        transport.Setup(t => t.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => sent.Add(m.Subject)).Returns(Task.CompletedTask);
        await queue.SendAsync(Message("notice") with { Category = EmailCategory.Notice }, CancellationToken.None);
        await queue.SendAsync(Message("link"), CancellationToken.None);
        using var dispatcher = new EmailDispatcher(queue, transport.Object, Options.Create(options), TimeProvider.System, NullLogger<EmailDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        for (var i = 0; i < 250 && sent.Count == 0; i++)
            await Task.Delay(20);
        await Task.Delay(200);
        await dispatcher.StopAsync(CancellationToken.None);

        sent.Should().Equal("notice");
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
        using var dispatcher = new EmailDispatcher(queue, transport.Object, Options.Create(Configured()), TimeProvider.System, NullLogger<EmailDispatcher>.Instance);

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
        using var dispatcher = new EmailDispatcher(queue, Mock.Of<IEmailTransport>(), Options.Create(Configured()), TimeProvider.System, NullLogger<EmailDispatcher>.Instance);

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
    [TestCase(null, SecureSocketOptions.StartTls)]
    [TestCase("nonsense", SecureSocketOptions.StartTls)]
    public void SecurityFor_MapsTheSetting(string? setting, SecureSocketOptions expected) =>
        SmtpEmailTransport.SecurityFor(setting).Should().Be(expected);

    [Test]
    public void Smtp_RequiresTlsByDefault() =>
        SmtpEmailTransport.SecurityFor(new SmtpOptions().Security).Should().Be(SecureSocketOptions.StartTls);

    [Test]
    public void QrCode_IsASquareSvgPathWithAQuietZone()
    {
        var image = new QrCodeRenderer().Render("otpauth://totp/MedApp:someone?secret=JBSWY3DPEHPK3PXP&issuer=MedApp");

        image.Size.Should().BeGreaterThan(21 + 2 * QrCodeRenderer.Border - 1);
        image.Path.Should().StartWith("M").And.MatchRegex("^[MHVhvZz0-9 ,.-]+$");
    }
}
