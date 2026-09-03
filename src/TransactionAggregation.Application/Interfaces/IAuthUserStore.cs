namespace TransactionAggregation.Application.Interfaces;

public interface IAuthUserStore
{
    Task<AuthUserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
}

public sealed record AuthUserAccount(
    Guid Id,
    string Username,
    string PasswordHash);
