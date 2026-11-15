using MedApp.API.Middleware;
using MedApp.API.Notifications;
using MedApp.API.ServerRendering;

namespace MedApp.API.Common.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApi(this WebApplication app)
    {
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

        // The page-rendering server's own calls to the API (SsrServer) may come over http on this machine.
        var ssr = app.Services.GetRequiredService<ISsrServer>();
        app.UseWhen(context => !ssr.IsFromRenderer(context), branch => branch.UseHttpsRedirection());

        // The page-rendering server and the build's manifest sit in wwwroot beside the app, but aren't for browsers.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/ssr") || context.Request.Path.StartsWithSegments("/.vite"))
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

        app.UseAuthentication();
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
}