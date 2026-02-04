using FluentValidation.AspNetCore;
using MedApp.API.Common.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace MedApp.API.Common.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApi(
        this IServiceCollection services)
    {
        services.AddControllers();
        services.AddFluentValidationAutoValidation();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddCors(options =>
        {
            options.AddPolicy("MedApp.Client", policy =>
            {
                policy
                    .WithOrigins("https://localhost:7292")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.AddApiAuthentication();

        return services;
    }

    private static IServiceCollection AddApiAuthentication(
        this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    SessionAuthenticationDefaults.Scheme;
                options.DefaultChallengeScheme =
                    SessionAuthenticationDefaults.Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                SessionAuthenticationDefaults.Scheme,
                _ => { });

        services.AddAuthorization();

        return services;
    }
}