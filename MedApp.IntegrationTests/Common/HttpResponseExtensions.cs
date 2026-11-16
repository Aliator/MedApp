using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace MedApp.IntegrationTests.Common;

public static class HttpResponseExtensions
{
    /// <summary>Asserts the status, showing the body when it's wrong (the problem details say why).</summary>
    public static async Task<HttpResponseMessage> ShouldHaveStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(expected,
                "{0} {1} answered with this body: {2}",
                response.RequestMessage?.Method, response.RequestMessage?.RequestUri?.PathAndQuery, body);
        }

        return response;
    }

    /// <summary>Asserts the status, then reads the body as JSON.</summary>
    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await response.ShouldHaveStatusAsync(expected);
        var value = await response.Content.ReadFromJsonAsync<T>();
        value.Should().NotBeNull();
        return value!;
    }

    /// <summary>A JSON request body; System.Net.Http.Json's DELETE has no body overload.</summary>
    public static Task<HttpResponseMessage> DeleteAsJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(body) });
}
