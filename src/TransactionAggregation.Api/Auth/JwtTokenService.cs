using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Infrastructure.Persistence;

namespace TransactionAggregation.Api.Auth;

public interface IJwtTokenService
{
    Task<LoginResult?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}

public sealed record LoginResult(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string Username);

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IAuthUserStore _authUserStore;
    private readonly IPasswordHasher<ServiceAccountIdentity> _passwordHasher;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        IAuthUserStore authUserStore,
        IPasswordHasher<ServiceAccountIdentity> passwordHasher,
        ILogger<JwtTokenService> logger)
    {
        _options = options.Value;
        _authUserStore = authUserStore;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<LoginResult?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await _authUserStore.FindByUsernameAsync(username.Trim(), cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("Login failed for username {Username}: user not found", username);
            return null;
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            new ServiceAccountIdentity { Username = user.Username },
            user.PasswordHash,
            password);

        if (verification == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login failed for username {Username}: invalid password", username);
            return null;
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var expiresMinutes = Math.Max(1, _options.ExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresIn = (int)TimeSpan.FromMinutes(expiresMinutes).TotalSeconds;

        _logger.LogInformation("Issued JWT for service account {Username}", user.Username);

        return new LoginResult(accessToken, "Bearer", expiresIn, user.Username);
    }
}
