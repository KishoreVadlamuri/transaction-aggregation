using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Options;

namespace TransactionAggregation.Infrastructure.Persistence;

/// <summary>
/// Marker type for <see cref="IPasswordHasher{TUser}"/>.
/// </summary>
public sealed class ServiceAccountIdentity
{
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Resolves the single configured service account from Docker/appsettings.
/// Uses the configured <c>PasswordHash</c> directly (no plaintext password in config).
/// </summary>
public sealed class ServiceAccountAuthStore : IAuthUserStore
{
    private readonly AuthUserAccount? _account;
    private readonly ILogger<ServiceAccountAuthStore> _logger;

    public ServiceAccountAuthStore(
        IOptions<ServiceAccountOptions> options,
        ILogger<ServiceAccountAuthStore> logger)
    {
        _logger = logger;
        _account = CreateAccount(options.Value, logger);
    }

    public Task<AuthUserAccount?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (_account is null)
        {
            _logger.LogWarning("Service account is not configured; login is unavailable");
            return Task.FromResult<AuthUserAccount?>(null);
        }

        if (!string.Equals(_account.Username, username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<AuthUserAccount?>(null);
        }

        return Task.FromResult<AuthUserAccount?>(_account);
    }

    internal static AuthUserAccount? CreateAccount(
        ServiceAccountOptions options,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.Username))
        {
            logger.LogError("ServiceAccount:Username is required");
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.PasswordHash))
        {
            logger.LogError(
                "ServiceAccount:PasswordHash is required (set ServiceAccount__PasswordHash in Docker/env or appsettings)");
            return null;
        }

        var username = options.Username.Trim();
        var passwordHash = options.PasswordHash.Trim();

        logger.LogInformation(
            "Service account {Username} loaded from PasswordHash config",
            username);

        return new AuthUserAccount(Guid.Empty, username, passwordHash);
    }
}
