using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

namespace TransactionAggregation.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured and at least 32 characters long.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Scalar users often paste "Bearer eyJ..." into the token box.
                        // The UI then sends "Authorization: Bearer Bearer eyJ...", which fails validation.
                        var header = context.Request.Headers.Authorization.ToString();
                        if (string.IsNullOrWhiteSpace(header))
                        {
                            return Task.CompletedTask;
                        }

                        const string bearerPrefix = "Bearer ";
                        if (!header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            return Task.CompletedTask;
                        }

                        var token = header[bearerPrefix.Length..].Trim();
                        while (token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            token = token[bearerPrefix.Length..].Trim();
                        }

                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("TransactionAggregation.Api.Jwt");
                        logger.LogWarning(
                            context.Exception,
                            "JWT authentication failed for {Method} {Path}",
                            context.Request.Method,
                            context.Request.Path);
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("TransactionAggregation.Api.Jwt");
                        logger.LogDebug(
                            "JWT challenge for {Method} {Path}: {Error} {ErrorDescription}",
                            context.Request.Method,
                            context.Request.Path,
                            context.Error,
                            context.ErrorDescription);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static Func<OpenApiDocument, OpenApiDocumentTransformerContext, CancellationToken, Task> CreateJwtOpenApiTransformer()
    {
        return (document, context, cancellationToken) =>
        {
            document.Components ??= new OpenApiComponents();
            document.AddComponent("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description =
                    "Paste the accessToken value only (do not include the word Bearer). " +
                    "Get a token from POST /api/v1/auth/login."
            });

            document.Security ??= [];
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });

            return Task.CompletedTask;
        };
    }
}

