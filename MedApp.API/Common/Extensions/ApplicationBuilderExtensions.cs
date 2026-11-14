using MedApp.API.Middleware;

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

        app.UseHttpsRedirection();

        // The built React app (MedApp.Web), which the AppHost copies into wwwroot on publish (plan Q4). In
        // development there's no wwwroot: Vite serves the app and proxies /api here.
        app.UseStaticFiles();

        app.UseMiddleware<ExceptionHandling>();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // An /api path that matches no endpoint is a 404, not the React app's index.html.
        app.Map("/api/{**path}", () => Results.NotFound());

        // Any other path is a client-side route (e.g. /patients/{id}), so it gets the React app.
        app.MapFallbackToFile("index.html");

        return app;
    }
}