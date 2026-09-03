namespace TransactionAggregation.Application.Options;

/// <summary>
/// Single service account used for JWT login. Username and password hash come from
/// configuration / Docker environment variables.
/// </summary>
public sealed class ServiceAccountOptions
{
    public const string SectionName = "ServiceAccount";

    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// ASP.NET Identity password hash (<c>ServiceAccount__PasswordHash</c>).
    /// Generate with <c>PasswordHasher</c>; login verifies plaintext against this hash.
    /// Never store the plaintext password in appsettings or Docker.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}
