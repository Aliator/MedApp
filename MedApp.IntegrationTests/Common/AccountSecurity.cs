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

    /// <summary>
    /// Gives the signed-in account <paramref name="address"/> (asked for with its <paramref name="password"/>), confirmed
    /// from the link emailed to it.
    /// </summary>
    public static async Task ConfirmAddressAsync(HttpClient client, string password, string address)
    {
        await (await client.PutAsJsonAsync("/api/auth/me/email", new ChangeEmailRequest(address, password)))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        await ConfirmFromEmailAsync(client, address);
    }

    /// <summary>The account and token in the newest confirmation link sent to <paramref name="address"/>.</summary>
    public static ConfirmEmailRequest ConfirmationLinkTo(string address)
    {
        var email = Api.Emails.To(address).Last(m => m.Subject == EmailTemplates.ConfirmAddressSubject);
        var (userId, token) = FakeEmailSender.LinkIn(email);
        return new ConfirmEmailRequest(userId, token);
    }

    /// <summary>
    /// Opens the newest confirmation link sent to <paramref name="address"/> with <paramref name="client"/>, which must
    /// be signed in as the account it's for.
    /// </summary>
    public static async Task ConfirmFromEmailAsync(HttpClient client, string address) =>
        await (await client.PostAsJsonAsync("/api/auth/email/confirm", ConfirmationLinkTo(address)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

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

    /// <summary>
    /// Sets up an authenticator and turns two-factor on for the signed-in account (whose <paramref name="password"/> it
    /// needs): its key and recovery codes.
    /// </summary>
    public static async Task<(string Key, IReadOnlyList<string> RecoveryCodes)> TurnOnTwoFactorAsync(HttpClient client, string password)
    {
        var setup = await (await client.PostAsync("/api/auth/me/two-factor/authenticator", null)).ReadAsAsync<AuthenticatorSetupResponse>();
        var codes = await (await client.PostAsJsonAsync("/api/auth/me/two-factor/enable", new TwoFactorCodeRequest(password, NextCode(setup.SharedKey))))
            .ReadAsAsync<RecoveryCodesResponse>();
        return (setup.SharedKey, codes.RecoveryCodes);
    }

    /// <summary>The newest two-factor code emailed to <paramref name="address"/>.</summary>
    public static string EmailedCodeTo(string address) =>
        FakeEmailSender.CodeIn(Api.Emails.To(address).Last(m => m.Subject == EmailTemplates.TwoFactorCodeSubject));

    /// <summary>How many two-factor code emails <paramref name="address"/> has had.</summary>
    public static int CodeEmailsTo(string address) =>
        Api.Emails.To(address).Count(m => m.Subject == EmailTemplates.TwoFactorCodeSubject);

    /// <summary>
    /// Gives the signed-in account (whose <paramref name="password"/> it needs) the confirmed <paramref name="address"/>,
    /// then turns two-factor on with codes by email, with the code emailed to it: its recovery codes.
    /// </summary>
    public static async Task<IReadOnlyList<string>> TurnOnEmailCodesAsync(HttpClient client, string password, string address)
    {
        await ConfirmAddressAsync(client, password, address);
        await (await client.PostAsJsonAsync("/api/auth/me/two-factor/email", new EmailCodesSetupRequest(password)))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        var codes = await (await client.PostAsJsonAsync("/api/auth/me/two-factor/email/enable", new EmailCodeRequest(EmailedCodeTo(address))))
            .ReadAsAsync<RecoveryCodesResponse>();
        return codes.RecoveryCodes;
    }

    /// <summary>
    /// Asks for a code to confirm a change, for the signed-in account using codes by email (its <paramref name="password"/>
    /// first), and returns the code emailed to <paramref name="address"/>.
    /// </summary>
    public static async Task<string> ConfirmCodeAsync(HttpClient client, string password, string address)
    {
        await (await client.PostAsJsonAsync("/api/auth/me/two-factor/email/code", new SendEmailCodeRequest(password)))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        return EmailedCodeTo(address);
    }

    /// <summary>
    /// Moves the signed-in account from codes by email to an authenticator app, proving the codes first with one emailed
    /// to <paramref name="address"/>: the app's key and the new recovery codes.
    /// </summary>
    public static async Task<(string Key, IReadOnlyList<string> RecoveryCodes)> SwitchToAppAsync(HttpClient client, string password, string address)
    {
        var current = await ConfirmCodeAsync(client, password, address);
        var setup = await (await client.PostAsync("/api/auth/me/two-factor/authenticator", null)).ReadAsAsync<AuthenticatorSetupResponse>();
        var codes = await (await client.PostAsJsonAsync("/api/auth/me/two-factor/enable",
                new TwoFactorCodeRequest(password, NextCode(setup.SharedKey), current)))
            .ReadAsAsync<RecoveryCodesResponse>();
        return (setup.SharedKey, codes.RecoveryCodes);
    }

    /// <summary>Signs in to an account using codes by email, both steps, with the code emailed to <paramref name="address"/>.</summary>
    public static async Task<HttpClient> SignInWithEmailCodeAsync(string username, string password, string address)
    {
        var client = Api.CreateApiClient();
        var challenge = await PasswordStepAsync(client, username, password);
        await (await client.PostAsJsonAsync("/api/auth/login/two-factor", new LoginTwoFactorRequest(challenge.Challenge, EmailedCodeTo(address))))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        return client;
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
