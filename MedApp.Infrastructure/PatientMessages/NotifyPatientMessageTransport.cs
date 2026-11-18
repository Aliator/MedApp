using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.PatientMessages;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.PatientMessages;

/// <summary>
/// GOV.UK Notify's settings (<c>PatientMessages:Notify</c>), for <see cref="PatientMessageMode.Live"/> only. The API key
/// is a secret: never in the repository, set only in the deployment's secret store.
/// </summary>
public sealed class NotifyOptions
{
    public const string Section = "PatientMessages:Notify";

    public string BaseUrl { get; set; } = "https://api.notifications.service.gov.uk";

    /// <summary>The service's API key (<c>{name}-{service ID}-{secret}</c>): a live key, or a test or team key while trying it.</summary>
    public string? ApiKey { get; set; }

    /// <summary>A text template whose whole message is <c>((message))</c>.</summary>
    public string? SmsTemplateId { get; set; }

    /// <summary>An email template whose subject is <c>((subject))</c> and body <c>((message))</c>.</summary>
    public string? EmailTemplateId { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public const string Incomplete =
        "PatientMessages:Mode is Live, so PatientMessages:Notify needs an ApiKey (name, service ID and secret) and both template IDs.";

    /// <summary>Whether Live can work: a key with a service ID and secret, and both templates.</summary>
    public bool IsComplete =>
        NotifyApiKey.TryParse(ApiKey, out _)
        && Guid.TryParse(SmsTemplateId, out _)
        && Guid.TryParse(EmailTemplateId, out _)
        && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

/// <summary>A Notify API key's parts: the key's name, then the service's ID and the secret (each a 36-character GUID).</summary>
public sealed record NotifyApiKey(string ServiceId, string Secret)
{
    public static bool TryParse(string? key, out NotifyApiKey parsed)
    {
        parsed = new NotifyApiKey(string.Empty, string.Empty);
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length < 74)
            return false;

        var trimmed = key.Trim();
        var serviceId = trimmed[^73..^37];
        var secret = trimmed[^36..];
        if (!Guid.TryParse(serviceId, out _) || !Guid.TryParse(secret, out _) || trimmed[^37] != '-')
            return false;

        parsed = new NotifyApiKey(serviceId, secret);
        return true;
    }
}

/// <summary>
/// Sends patient messages through GOV.UK Notify's REST API (v2): a text to the mobile, or an email to the address, with
/// one template each whose personalisation is the whole message (and an email's subject), and the message's ID as
/// Notify's reference. Each request carries a JWT (HS256, the service ID as issuer, signed with the secret), made here
/// to need no client library. Its responses are never logged: they can quote the number or address.
/// <list type="bullet">
/// <item>400 (an invalid number or address, say): rejected, with Notify's error code; it won't go if tried again.</item>
/// <item>429 (rate or daily limit): the rest wait for the next run.</item>
/// <item>403 and 5xx: thrown, so the job counts an attempt and tries again later.</item>
/// <item>A timeout: unconfirmed (it may have been taken), never sent again. Its client has no resilience handler, so
/// nothing retries a POST inside either (Notify has no idempotency key).</item>
/// </list>
/// Delivery comes from <c>GET /v2/notifications/{id}</c>: <c>delivered</c>, the three failures, or still on its way.
/// </summary>
public sealed class NotifyPatientMessageTransport(IHttpClientFactory clients, IOptions<NotifyOptions> options, TimeProvider timeProvider)
    : IPatientMessageTransport
{
    /// <summary>Its named HttpClient, from which the API removes the default resilience handler.</summary>
    public const string ClientName = "notify";

    private HttpClient Http() => clients.CreateClient(ClientName);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PatientMessageMode Mode => PatientMessageMode.Live;

    public async Task<PatientMessageSendResult> SendAsync(PatientMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        var (path, body) = message.Channel == PatientMessageChannel.Text
            ? ("v2/notifications/sms", (object)new SmsRequest(
                message.Destination,
                settings.SmsTemplateId!,
                new Dictionary<string, string> { ["message"] = message.Body },
                message.Id.ToString()))
            : ("v2/notifications/email", new EmailRequest(
                message.Destination,
                settings.EmailTemplateId!,
                new Dictionary<string, string>
                {
                    ["subject"] = PatientMessageTexts.Subject(message.Kind),
                    ["message"] = message.Body
                },
                message.Id.ToString()));

        using var request = new HttpRequestMessage(HttpMethod.Post, Url(path))
        {
            Content = JsonContent.Create(body, body.GetType(), options: Json)
        };
        Authorise(request);

        HttpResponseMessage response;
        try
        {
            response = await Http().SendAsync(request, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient's timeout: the request may have reached Notify and been taken.
            return PatientMessageSendResult.Unconfirmed();
        }

        using var _ = response;
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return PatientMessageSendResult.TryLater();

        if (response.StatusCode == HttpStatusCode.BadRequest)
            return PatientMessageSendResult.Rejected(await ErrorCodeAsync(response, ct));

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Notify answered {(int)response.StatusCode}.", null, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<NotificationCreated>(Json, ct);
        return created?.Id is { Length: > 0 } id
            ? PatientMessageSendResult.Accepted(id)
            : throw new HttpRequestException("Notify's answer had no notification ID.");
    }

    public async Task<PatientMessageDelivery> GetDeliveryAsync(string providerReference, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url($"v2/notifications/{Uri.EscapeDataString(providerReference)}"));
        Authorise(request);

        using var response = await Http().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Notify answered {(int)response.StatusCode}.", null, response.StatusCode);

        var notification = await response.Content.ReadFromJsonAsync<NotificationStatus>(Json, ct);
        return DeliveryFor(notification?.Status);
    }

    /// <summary>Notify's status in MedApp's terms: delivered, failed (keeping Notify's name for it) or still on its way.</summary>
    public static PatientMessageDelivery DeliveryFor(string? status) => status switch
    {
        "delivered" => PatientMessageDelivery.Delivered,
        "permanent-failure" or "temporary-failure" or "technical-failure" or "validation-failed" or "virus-scan-failed" =>
            PatientMessageDelivery.Failed(status),
        _ => PatientMessageDelivery.OnItsWay
    };

    /// <summary>
    /// The JWT Notify asks for: <c>{"typ":"JWT","alg":"HS256"}</c>, then <c>{"iss":service ID,"iat":now in seconds}</c>,
    /// signed with the secret (HMAC-SHA256), each part base64url without padding.
    /// </summary>
    public static string Token(NotifyApiKey key, DateTimeOffset now)
    {
        var header = Base64Url("{\"typ\":\"JWT\",\"alg\":\"HS256\"}"u8.ToArray());
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = key.ServiceId, iat = now.ToUnixTimeSeconds() }));
        var signed = $"{header}.{claims}";
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key.Secret), Encoding.UTF8.GetBytes(signed));
        return $"{signed}.{Base64Url(signature)}";
    }

    private void Authorise(HttpRequestMessage request)
    {
        if (!NotifyApiKey.TryParse(options.Value.ApiKey, out var key))
            throw new InvalidOperationException(NotifyOptions.Incomplete);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(key, timeProvider.GetUtcNow()));
    }

    private Uri Url(string path) => new(new Uri(options.Value.BaseUrl.TrimEnd('/') + "/"), path);

    /// <summary>Notify's error code from a refusal (e.g. <c>ValidationError</c>), never its message, which can quote the destination.</summary>
    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<NotifyError>(Json, ct);
            var code = error?.Errors?.FirstOrDefault()?.Error;
            return string.IsNullOrWhiteSpace(code) || code.Length > 50 || !code.All(char.IsLetterOrDigit)
                ? PatientMessageReasons.Rejected
                : code;
        }
        catch (JsonException)
        {
            return PatientMessageReasons.Rejected;
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record SmsRequest(
        [property: JsonPropertyName("phone_number")] string PhoneNumber,
        [property: JsonPropertyName("template_id")] string TemplateId,
        [property: JsonPropertyName("personalisation")] Dictionary<string, string> Personalisation,
        [property: JsonPropertyName("reference")] string Reference);

    private sealed record EmailRequest(
        [property: JsonPropertyName("email_address")] string EmailAddress,
        [property: JsonPropertyName("template_id")] string TemplateId,
        [property: JsonPropertyName("personalisation")] Dictionary<string, string> Personalisation,
        [property: JsonPropertyName("reference")] string Reference);

    private sealed record NotificationCreated(string? Id);

    private sealed record NotificationStatus(string? Id, string? Status);

    private sealed record NotifyError(List<NotifyErrorItem>? Errors);

    private sealed record NotifyErrorItem(string? Error);
}
