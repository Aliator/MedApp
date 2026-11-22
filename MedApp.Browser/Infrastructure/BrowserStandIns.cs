using System.Security.Claims;
using MedApp.API.Common.Authentication;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Identity
{
    /// <summary>
    /// Password reset tokens without ASP.NET Core's data protection (not in the browser): Identity's time-based codes,
    /// under the API's provider name. Nothing is emailed in the browser, so no link carries one.
    /// </summary>
    public sealed class PasswordResetTokenProvider : TotpSecurityStampBasedTokenProvider<ApplicationUser>
    {
        public const string ProviderName = "PasswordReset";

        public override Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult(false);
    }
}

namespace MedApp.Browser.Infrastructure
{
    /// <summary>One person at a time: a calendar change needs no lock, only its transaction, so a clash saves nothing.</summary>
    public sealed class BrowserCalendarLock(MedAppDbContext context) : ICalendarLock
    {
        public async Task<T> RunAsync<T>(IReadOnlyCollection<Guid> staffUserIds, Func<CancellationToken, Task<T>> work, CancellationToken ct)
        {
            if (context.Database.CurrentTransaction is not null)
                return await work(ct);

            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
    }

    /// <summary>
    /// Signing in is choosing an account (the login page's account picker): any password is taken, and there's no
    /// two-factor step, as the visitor owns every account of the synthetic clinic in their browser.
    /// </summary>
    public sealed class BrowserAuthenticationService(UserManager<ApplicationUser> users) : IAuthenticationService
    {
        public async Task<(Guid UserId, string Username, IEnumerable<string> Roles)> ValidateCredentialsAsync(string username, string password)
        {
            var user = await users.FindByNameAsync(username.Trim()) ?? throw new InvalidCredentialsException();
            return (user.Id, user.UserName!, await users.GetRolesAsync(user));
        }
    }

    /// <summary>The one session: what the API keeps in a cookie, kept in memory, so it ends when the page is closed.</summary>
    public sealed class BrowserSessionCookieService : ISessionCookieService
    {
        private Guid? _sessionId;

        public string CookieName => SessionAuthenticationDefaults.HostCookieName;

        public void AppendSessionCookie(HttpResponse response, LoginResult session) => _sessionId = session.SessionId;

        public void DeleteSessionCookie(HttpResponse response) => _sessionId = null;

        public bool TryGetSessionCookie(HttpRequest request, out Guid? sessionId)
        {
            sessionId = _sessionId;
            return _sessionId is not null;
        }
    }

    /// <summary>The activity log's actor: the request's signed-in user, with no address (it's the visitor's browser).</summary>
    public sealed class BrowserActivityContext(IHttpContextAccessor accessor) : IActivityContext
    {
        public ActivityActor Actor
        {
            get
            {
                var user = accessor.HttpContext?.User;
                var isSignedIn = user?.Identity?.IsAuthenticated == true;
                Guid? userId = isSignedIn && Guid.TryParse(user!.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
                return new ActivityActor(userId, isSignedIn ? user!.Identity!.Name ?? string.Empty : string.Empty, null);
            }
        }
    }

    /// <summary>No live notifications in the browser: the page reads them when it asks.</summary>
    public sealed class NoNotificationPublisher : INotificationPublisher
    {
        public Task PublishAsync(Guid userId, NotificationResponse notification, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>No email from the browser, as with no mail server set up: the app says so where it matters.</summary>
    public sealed class NoEmailSender : IEmailSender
    {
        public bool CanSend => false;

        public Task SendAsync(EmailMessage message, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>No secure email relay in the browser: letters can't be sent that way, as when none is set up.</summary>
    public sealed class NoLetterTransport : ILetterTransport
    {
        public bool IsAvailable => false;

        public Task<string> SendAsync(SecureLetterEmail email, CancellationToken ct) =>
            throw new InvalidOperationException("Letters can't be sent by secure email in the browser.");
    }

    /// <summary>No thumbnails (SkiaSharp isn't built for the browser): images are listed without one.</summary>
    public sealed class NoThumbnails : IThumbnailGenerator
    {
        public Task<byte[]?> CreateAsync(Stream image, AttachmentFileType type, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
}
