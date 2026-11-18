using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Common.Email;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.Infrastructure.Common.Email;
using MedApp.Infrastructure.PatientMessages;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.PatientMessagesTestConstants;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace MedApp.UnitTests.Infrastructure.PatientMessages;

[TestFixture]
public sealed class PatientMessageTransportTests
{
    // A synthetic key in Notify's shape: a name, then the service ID and the secret.
    private const string ServiceId = "26785a09-ab16-4eb0-8407-a37497a57506";
    private const string Secret = "3d844edf-8d35-48ac-975b-e847b4f122b0";
    private const string ApiKey = "medapp_test-" + ServiceId + "-" + Secret;
    private const string SmsTemplate = "f33517ff-2a88-4f6e-b855-c550268ce08a";
    private const string EmailTemplate = "4e1f6bfa-8f3f-4e23-9a2c-4e8dc4ef7d5a";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return answer(request);
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private static NotifyPatientMessageTransport Notify(HttpMessageHandler handler) =>
        new(new StubFactory(handler),
            MsOptions.Create(new NotifyOptions { ApiKey = ApiKey, SmsTemplateId = SmsTemplate, EmailTemplateId = EmailTemplate }),
            new FixedTimeProvider(Now));

    private static PatientMessage Text(PatientMessageChannel channel = PatientMessageChannel.Text)
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        return Message(appointment, PatientMessageKind.AppointmentReminder, channel);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Test]
    public void ApiKey_ParsesTheServiceIdAndSecret()
    {
        NotifyApiKey.TryParse(ApiKey, out var key).Should().BeTrue();
        key.ServiceId.Should().Be(ServiceId);
        key.Secret.Should().Be(Secret);
        NotifyApiKey.TryParse("not-a-key", out _).Should().BeFalse();
        NotifyApiKey.TryParse(null, out _).Should().BeFalse();
    }

    [Test]
    public void Options_LiveNeedsAKeyAndBothTemplates()
    {
        new NotifyOptions { ApiKey = ApiKey, SmsTemplateId = SmsTemplate, EmailTemplateId = EmailTemplate }.IsComplete.Should().BeTrue();
        new NotifyOptions { ApiKey = ApiKey, SmsTemplateId = SmsTemplate }.IsComplete.Should().BeFalse();
        new NotifyOptions { SmsTemplateId = SmsTemplate, EmailTemplateId = EmailTemplate }.IsComplete.Should().BeFalse();
        new NotifyOptions { ApiKey = ApiKey, SmsTemplateId = SmsTemplate, EmailTemplateId = EmailTemplate, BaseUrl = "http://insecure" }
            .IsComplete.Should().BeFalse();
    }

    [Test]
    public void Token_IsAnHs256JwtWithTheServiceAsIssuer()
    {
        NotifyApiKey.TryParse(ApiKey, out var key);

        var token = NotifyPatientMessageTransport.Token(key, new DateTimeOffset(Now));

        var parts = token.Split('.');
        parts.Should().HaveCount(3);
        Decode(parts[0]).Should().Be("{\"typ\":\"JWT\",\"alg\":\"HS256\"}");
        using var claims = JsonDocument.Parse(Decode(parts[1]));
        claims.RootElement.GetProperty("iss").GetString().Should().Be(ServiceId);
        claims.RootElement.GetProperty("iat").GetInt64().Should().Be(new DateTimeOffset(Now).ToUnixTimeSeconds());
        var expected = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        parts[2].Should().Be(expected);
    }

    private static string Decode(string part)
    {
        var padded = part.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    [Test]
    public async Task Send_Text_PostsTheMessageAsPersonalisation_AndKeepsNotifysId()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Created, "{\"id\":\"740e5834-3a29-46b4-9a6f-16142fde533a\"}"));
        var message = Text();

        var result = await Notify(handler).SendAsync(message, CancellationToken.None);

        result.Outcome.Should().Be(PatientMessageSendOutcome.Accepted);
        result.Reference.Should().Be("740e5834-3a29-46b4-9a6f-16142fde533a");
        var (request, body) = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://api.notifications.service.gov.uk/v2/notifications/sms");
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("phone_number").GetString().Should().Be(Mobile);
        json.RootElement.GetProperty("template_id").GetString().Should().Be(SmsTemplate);
        json.RootElement.GetProperty("personalisation").GetProperty("message").GetString().Should().Be(message.Body);
        json.RootElement.GetProperty("reference").GetString().Should().Be(message.Id.ToString());
    }

    [Test]
    public async Task Send_Email_SendsTheSubjectToo()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Created, "{\"id\":\"abc\"}"));

        await Notify(handler).SendAsync(Text(PatientMessageChannel.Email), CancellationToken.None);

        var (request, body) = handler.Requests.Single();
        request.RequestUri!.AbsolutePath.Should().Be("/v2/notifications/email");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("email_address").GetString().Should().Be(Email);
        json.RootElement.GetProperty("template_id").GetString().Should().Be(EmailTemplate);
        json.RootElement.GetProperty("personalisation").GetProperty("subject").GetString().Should().Be("Appointment reminder");
    }

    [Test]
    public async Task Send_BadRequest_IsRejectedWithNotifysErrorCodeOnly()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest,
            "{\"errors\":[{\"error\":\"ValidationError\",\"message\":\"phone_number +447700900123 is not valid\"}],\"status_code\":400}"));

        var result = await Notify(handler).SendAsync(Text(), CancellationToken.None);

        result.Outcome.Should().Be(PatientMessageSendOutcome.Rejected);
        result.Reason.Should().Be("ValidationError");
    }

    [Test]
    public async Task Send_TooManyRequests_TriesLater()
    {
        var result = await Notify(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))).SendAsync(Text(), CancellationToken.None);

        result.Outcome.Should().Be(PatientMessageSendOutcome.TryLater);
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task Send_OtherFailures_Throw(HttpStatusCode status)
    {
        var act = () => Notify(new StubHandler(_ => new HttpResponseMessage(status))).SendAsync(Text(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public async Task Send_Timeout_IsUnconfirmed()
    {
        var result = await Notify(new ThrowingHandler(new TaskCanceledException("timeout", new TimeoutException())))
            .SendAsync(Text(), CancellationToken.None);

        result.Outcome.Should().Be(PatientMessageSendOutcome.Unconfirmed);
    }

    [Test]
    public async Task Send_NoConnection_Throws_SoTheJobMayTryAgain()
    {
        var act = () => Notify(new ThrowingHandler(new HttpRequestException("refused"))).SendAsync(Text(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public async Task Offline_UnsafeMailbox_RefusesToSend()
    {
        var options = Options();
        options.OfflineRecipient = "someone@example.com";
        var transport = new OfflinePatientMessageTransport(
            new Mock<IEmailTransport>().Object, MsOptions.Create(new EmailDeliveryOptions()), MsOptions.Create(options));

        var act = () => transport.SendAsync(Text(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestCase("patient-messages@medapp.invalid", true)]
    [TestCase("messages@clinic.test", true)]
    [TestCase("messages@localhost", true)]
    [TestCase("messages@mailpit.localhost", true)]
    [TestCase("someone@example.com", false)]
    [TestCase("someone@nhs.net", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void Options_OfflineMailbox_MustBeOneNoMailServerDelivers(string? address, bool safe)
    {
        PatientMessageOptions.IsSafeOfflineRecipient(address).Should().Be(safe);
        new PatientMessageOptions { Mode = PatientMessageMode.Offline, OfflineRecipient = address! }.IsValid.Should().Be(safe);
        new PatientMessageOptions { Mode = PatientMessageMode.Off, OfflineRecipient = address! }.IsValid.Should().BeTrue();
    }

    [TestCase("delivered", PatientMessageStatus.Delivered)]
    [TestCase("permanent-failure", PatientMessageStatus.Failed)]
    [TestCase("temporary-failure", PatientMessageStatus.Failed)]
    [TestCase("technical-failure", PatientMessageStatus.Failed)]
    [TestCase("sending", PatientMessageStatus.Sent)]
    [TestCase("created", PatientMessageStatus.Sent)]
    [TestCase(null, PatientMessageStatus.Sent)]
    public void DeliveryFor_NotifysStatuses(string? status, PatientMessageStatus expected) =>
        NotifyPatientMessageTransport.DeliveryFor(status).Status.Should().Be(expected);

    [Test]
    public async Task GetDelivery_AsksForTheNotification()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{\"id\":\"abc\",\"status\":\"permanent-failure\"}"));

        var delivery = await Notify(handler).GetDeliveryAsync("abc", CancellationToken.None);

        delivery.Status.Should().Be(PatientMessageStatus.Failed);
        delivery.Reason.Should().Be("permanent-failure");
        handler.Requests.Single().Request.RequestUri!.AbsolutePath.Should().Be("/v2/notifications/abc");
    }

    [Test]
    public async Task Offline_WithAMailServer_EmailsTheOfflineMailboxNeverThePatient()
    {
        var email = new Mock<IEmailTransport>();
        EmailMessage? sent = null;
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback((EmailMessage m, CancellationToken _) => sent = m)
            .Returns(Task.CompletedTask);
        var transport = new OfflinePatientMessageTransport(
            email.Object,
            MsOptions.Create(new EmailDeliveryOptions { FromAddress = "medapp@localhost", Smtp = { Host = "localhost" } }),
            MsOptions.Create(Options()));
        var message = Text();

        var result = await transport.SendAsync(message, CancellationToken.None);

        transport.Mode.Should().Be(PatientMessageMode.Offline);
        sent!.To.Should().Be("patient-messages@medapp.invalid");
        sent.Subject.Should().Be($"[Text to {Mobile}] Appointment reminder");
        sent.TextBody.Should().Be(message.Body);
        (await transport.GetDeliveryAsync(result.Reference!, CancellationToken.None)).Should().Be(PatientMessageDelivery.Delivered);
    }

    [Test]
    public async Task Offline_WithoutAMailServer_OnlyMarksItSent_AndTheSyntheticFailingNumberFails()
    {
        var email = new Mock<IEmailTransport>();
        var transport = new OfflinePatientMessageTransport(email.Object, MsOptions.Create(new EmailDeliveryOptions()), MsOptions.Create(Options()));
        var message = Text();
        message.Destination = FailingMobile;

        var result = await transport.SendAsync(message, CancellationToken.None);

        email.VerifyNoOtherCalls();
        result.Outcome.Should().Be(PatientMessageSendOutcome.Accepted);
        (await transport.GetDeliveryAsync(result.Reference!, CancellationToken.None)).Status.Should().Be(PatientMessageStatus.Failed);
        OfflinePatientMessageTransport.FailsFor("pat+fail@example.com").Should().BeTrue();
        OfflinePatientMessageTransport.FailsFor(Email).Should().BeFalse();
    }

    [Test]
    public async Task Off_RefusesToSend()
    {
        var transport = new NoPatientMessageTransport();

        transport.Mode.Should().Be(PatientMessageMode.Off);
        await ((Func<Task>)(() => transport.SendAsync(Text(), CancellationToken.None))).Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => transport.GetDeliveryAsync("x", CancellationToken.None))).Should().ThrowAsync<InvalidOperationException>();
    }
}
