using System.Text;
using FluentValidation.AspNetCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MedApp.API.Common.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();
        services.AddFluentValidationAutoValidation();
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token without the 'Bearer ' prefix"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        services.AddCors(options =>
        {
            options.AddPolicy("Client", policy =>
            {
                policy
                    .WithOrigins("https://localhost:7292")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddApiAuthentication(configuration);

        return services;
    }

    private static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication("Bearer")
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
                };
            });

        services.AddAuthorization();

        return services;
    }
}