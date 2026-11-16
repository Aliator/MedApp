using MedApp.API.Common.Authentication;
using MedApp.API.Middleware;
using MedApp.API.Notifications;
using MedApp.API.ServerRendering;

namespace MedApp.API.Common.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApi(this WebApplication app)
    {
        // First, so every response carries them: nosniff, no framing, the referrer and permissions policies, no-store
        // on /api, and the Content Security Policies (with the page's nonce, which SsrPages passes to the renderer).
        app.UseMiddleware<SecurityHeaders>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            // Browsers stay on https for this host once they've seen it there (sent only on https, never localhost).
            app.UseHsts();
        }

        // No https redirect for requests the API received on a loopback address: only something on this machine (in
        // Kubernetes, in this pod) can connect there, such as the page server calling for a page's data over http, and
        // forwarded headers don't change the local address. So an https port can't silently break server rendering.
        // A page server the API started also carries its key (SsrServer.IsFromRenderer).
        var ssr = app.Services.GetRequiredService<ISsrServer>();
        app.UseWhen(
            context => !ssr.IsFromRenderer(context) && !IsLoopback(context.Connection.LocalIpAddress),
            branch => branch.UseHttpsRedirection());

        // Paths no page or file of the app has (PageServerFiles): any path with an empty segment (//), and, as defence
        // in depth, /ssr and /.vite, where the page server and the build's manifest used to sit in wwwroot.
        app.Use(async (context, next) =>
        {
            if (PageServerFiles.IsBlocked(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next();
        });

        // The built React app (MedApp.Web), which the AppHost copies into wwwroot on publish (plan Q4). In
        // development there's no wwwroot: Vite serves the app and proxies /api here.
        app.UseStaticFiles();

        app.UseMiddleware<ExceptionHandling>();

        // Sign-in throttling (LoginThrottling): before authentication, so a refused attempt costs no session lookup.
        app.UseRateLimiter();

        app.UseAuthentication();
        // A user with a password an admin gave them can only change it (and sign out) until they do.
        app.UseMiddleware<PasswordChangeRequired>();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<NotificationsHub>(NotificationsHub.Path);

        // An /api path that matches no endpoint is a 404, not the React app's index.html.
        app.Map("/api/{**path}", () => Results.NotFound());

        // Any other path (that isn't a file) is one of the app's pages (e.g. /patients/{id}): rendered on the server
        // when it can be, otherwise the client-rendered app.
        app.MapFallback("{*path:nonfile}", SsrPages.Handle);

        return app;
    }

    private static bool IsLoopback(System.Net.IPAddress? address) =>
        address is not null && System.Net.IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);
}