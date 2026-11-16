using Microsoft.Extensions.Options;

namespace MedApp.Application.Common.Email;

/// <summary>
/// The links emails carry, from <see cref="EmailOptions.LinkBaseUrl"/>. A token goes in the link's fragment (after
/// <c>#</c>), which browsers never send to a server, so it stays out of access logs, proxies and the Referer header; the
/// page reads it in the browser.
/// </summary>
public sealed class EmailLinks(IOptions<EmailOptions> options)
{
    public const string ResetPasswordPath = "/reset-password";
    public const string ConfirmEmailPath = "/confirm-email";
    public const string SignInPath = "/login";

    /// <summary>Whether links can be made (a base address is configured).</summary>
    public bool CanLink => BaseUrl is not null;

    /// <summary>The "choose a new password" page, for this account and reset token; null without a base address.</summary>
    public string? ResetPassword(Guid userId, string token) => WithToken(ResetPasswordPath, userId, token);

    /// <summary>The page that confirms an email address, for this account and token; null without a base address.</summary>
    public string? ConfirmEmail(Guid userId, string token) => WithToken(ConfirmEmailPath, userId, token);

    /// <summary>The sign-in page; null without a base address.</summary>
    public string? SignIn() => BaseUrl is { } baseUrl ? baseUrl + SignInPath : null;

    private string? WithToken(string path, Guid userId, string token) =>
        BaseUrl is { } baseUrl
            ? $"{baseUrl}{path}#user={userId:D}&token={Uri.EscapeDataString(token)}"
            : null;

    private string? BaseUrl =>
        string.IsNullOrWhiteSpace(options.Value.LinkBaseUrl) ? null : options.Value.LinkBaseUrl.Trim().TrimEnd('/');
}
