using TransactionAggregation.Api.Auth;
using TransactionAggregation.Api.Models;

namespace TransactionAggregation.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Login and get a JWT")
            .WithDescription(
                "Validates the configured service account (ServiceAccount username + PasswordHash from " +
                "Docker env or appsettings). Login sends plaintext password; it is verified against the " +
                "configured hash (not stored in PostgreSQL). A valid token can call any protected endpoint.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IJwtTokenService tokenService,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("TransactionAggregation.Api.Auth");

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new ApiError
            {
                Message = "Username and password are required.",
                TraceId = httpContext.TraceIdentifier
            });
        }

        var result = await tokenService.LoginAsync(request.Username.Trim(), request.Password, cancellationToken);
        if (result is null)
        {
            logger.LogWarning("HTTP login rejected for {Username}", request.Username);
            return Results.Json(
                new ApiError
                {
                    Message = "Invalid username or password.",
                    TraceId = httpContext.TraceIdentifier
                },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(new LoginResponse
        {
            AccessToken = result.AccessToken,
            TokenType = result.TokenType,
            ExpiresIn = result.ExpiresInSeconds,
            Username = result.Username
        });
    }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public required string AccessToken { get; init; }
    public required string TokenType { get; init; }
    public required int ExpiresIn { get; init; }
    public required string Username { get; init; }
}
