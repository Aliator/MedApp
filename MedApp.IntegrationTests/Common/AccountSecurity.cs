using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Common.Email;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Infrastructure.Common.Identity;

namespace MedApp.IntegrationTests.Common;

/// <summary>
/// Helpers for the email, password reset and two-factor tests, all through the API as people use it: confirming an
/// address from the link emailed to it, setting up an authenticator from the key the API shows, and making codes as an
/// authenticator app would, from the test clock.
/// </summary>
public static class AccountSecurity
{
    private static MedAppApiFactory Api => TestEnvironment.Api;

    /// <summary>A synthetic address, new each time.</summary>
    public static string NewAddress() => $"{Guid.NewGuid():N}@example.test";

    /// <summary>Gives the signed-in account <paramref name="address"/>, confirmed from the link emailed to it.</summary>
    public static async Task ConfirmAddressAsync(HttpClient client, string address)
    {
        await (await client.PutAsJsonAsync("/api/auth/me/email", new ChangeEmailRequest(address)))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        await ConfirmFromEmailAsync(address);
    }

    /// <summary>Opens the newest confirmation link sent to <paramref name="address"/>, signed out, as from an email app.</summary>
    public static async Task ConfirmFromEmailAsync(string address)
    {
        var email = Api.Emails.To(address).Last(m => m.Subject == EmailTemplates.ConfirmEmailSubject);
        var (userId, token) = FakeEmailSender.LinkIn(email);
        await (await Api.CreateApiClient().PostAsJsonAsync("/api/auth/email/confirm", new ConfirmEmailRequest(userId, token)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// The authenticator code for <paramref name="sharedKey"/> (as the API shows it, in groups) at the next 30-second
    /// step: the test clock moves on first, so each code is new, as each code an app shows is.
    /// </summary>
    public static string NextCode(string sharedKey)
    {
        Api.Clock.Advance(Totp.Step);
        return CodeNow(sharedKey);
    }

    /// <summary>The code for the step the test clock is in now.</summary>
    public static string CodeNow(string sharedKey) =>
        Totp.Compute(Base32.Decode(sharedKey.Replace(" ", string.Empty)), Totp.TimeStep(Api.Clock.GetUtcNow()));

    /// <summary>Sets up an authenticator and turns two-factor on for the signed-in account: its key and recovery codes.</summary>
    public static async Task<(string Key, IReadOnlyList<string> RecoveryCodes)> TurnOnTwoFactorAsync(HttpClient client)
    {
        var setup = await (await client.PostAsync("/api/auth/me/two-factor/authenticator", null)).ReadAsAsync<AuthenticatorSetupResponse>();
        var codes = await (await client.PostAsJsonAsync("/api/auth/me/two-factor/enable", new TwoFactorCodeRequest(NextCode(setup.SharedKey))))
            .ReadAsAsync<RecoveryCodesResponse>();
        return (setup.SharedKey, codes.RecoveryCodes);
    }

    /// <summary>The password step for an account with two-factor: the challenge (a 202, no session).</summary>
    public static async Task<TwoFactorChallengeResponse> PasswordStepAsync(HttpClient client, string username, string password) =>
        await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password)))
            .ReadAsAsync<TwoFactorChallengeResponse>(HttpStatusCode.Accepted);

    /// <summary>Signs in to an account with two-factor, both steps, with the next authenticator code.</summary>
    public static async Task<HttpClient> SignInWithTwoFactorAsync(string username, string password, string key)
    {
        var client = Api.CreateApiClient();
        var challenge = await PasswordStepAsync(client, username, password);
        await (await client.PostAsJsonAsync("/api/auth/login/two-factor", new LoginTwoFactorRequest(challenge.Challenge, NextCode(key))))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        return client;
    }

    /// <summary>The <c>code</c> of a problem response.</summary>
    public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>Asserts that the client's session works (or not).</summary>
    public static async Task ShouldBeSignedInAsync(HttpClient client, bool signedIn = true)
    {
        var whoAmI = await (await client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        whoAmI.IsAuthenticated.Should().Be(signedIn);
    }
}
