using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TransactionAggregation.Api.Auth;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Infrastructure.Persistence;
using Xunit;

namespace TransactionAggregation.UnitTests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public async Task Login_ServiceAccount_ReturnsToken()
    {
        var password = NewTestPassword();
        var username = ServiceAccountUsername();
        var service = CreateService(password);

        var result = await service.LoginAsync(username, password);

        Assert.NotNull(result);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(username, result.Username);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Fact]
    public async Task Login_RejectsBadPassword()
    {
        var service = CreateService(NewTestPassword());
        Assert.Null(await service.LoginAsync(ServiceAccountUsername(), NewTestPassword()));
    }

    [Fact]
    public async Task Login_RejectsUnknownUsername()
    {
        var password = NewTestPassword();
        Assert.Null(await CreateService(password).LoginAsync("other.user", password));
    }

    private static string NewTestPassword() => $"test-{Guid.NewGuid():N}";

    private static string ServiceAccountUsername() =>
        TestConfiguration.GetOptions<ServiceAccountOptions>(ServiceAccountOptions.SectionName).Username;

    private static JwtTokenService CreateService(string password)
    {
        var username = ServiceAccountUsername();
        var hasher = new PasswordHasher<ServiceAccountIdentity>();
        var options = Options.Create(new ServiceAccountOptions
        {
            Username = username,
            // Per-test password so login assertions stay isolated from the shared appsettings hash.
            PasswordHash = hasher.HashPassword(
                new ServiceAccountIdentity { Username = username },
                password)
        });

        var store = new ServiceAccountAuthStore(
            options,
            NullLogger<ServiceAccountAuthStore>.Instance);

        var jwt = TestConfiguration.CreateOptions<JwtOptions>(JwtOptions.SectionName);

        return new JwtTokenService(jwt, store, hasher, NullLogger<JwtTokenService>.Instance);
    }
}

public sealed class ServiceAccountAuthStoreTests
{
    [Fact]
    public async Task CreateAccount_UsesConfiguredPasswordHash()
    {
        var password = $"test-{Guid.NewGuid():N}";
        var username = TestConfiguration
            .GetOptions<ServiceAccountOptions>(ServiceAccountOptions.SectionName)
            .Username;
        var hasher = new PasswordHasher<ServiceAccountIdentity>();
        var passwordHash = hasher.HashPassword(
            new ServiceAccountIdentity { Username = username },
            password);

        var store = new ServiceAccountAuthStore(
            Options.Create(new ServiceAccountOptions
            {
                Username = username,
                PasswordHash = passwordHash
            }),
            NullLogger<ServiceAccountAuthStore>.Instance);

        var account = await store.FindByUsernameAsync(username);
        Assert.NotNull(account);
        Assert.Equal(passwordHash, account.PasswordHash);

        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(
                new ServiceAccountIdentity { Username = account.Username },
                account.PasswordHash,
                password));
    }

    [Fact]
    public async Task CreateAccount_RequiresPasswordHash()
    {
        var username = TestConfiguration
            .GetOptions<ServiceAccountOptions>(ServiceAccountOptions.SectionName)
            .Username;
        var store = new ServiceAccountAuthStore(
            Options.Create(new ServiceAccountOptions
            {
                Username = username,
                PasswordHash = string.Empty
            }),
            NullLogger<ServiceAccountAuthStore>.Instance);

        Assert.Null(await store.FindByUsernameAsync(username));
    }
}
